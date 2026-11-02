-- G-K 2B — Build tiêu thức NVL (allocate_type=0) chống nhân đôi
-- MySQL 5.6 / 8.0 · DEV/TEST only
--
-- Quy tắc:
-- 1) Gắn qua BOM: finished → material → OUTPUT_ID → chitdetail (N621/C152)
-- 2) Nhiều material cùng OUTPUT_ID → split theo CONFIRMED_QUANTITY
-- 3) Nhiều OUTPUT cùng CHITDETAIL_ID → chia AMOUNT chứng từ theo tỷ lệ AMOUNT_CC output
--    (không cộng full AMOUNT cho mỗi OUTPUT)
-- 4) Không BOM → orphan, không vào basis

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_nvl_basis_build`$$
CREATE PROCEDURE `sp_pc_nvl_basis_build`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_RUN_ID BIGINT,
    IN p_RUN_VERSION INT,
    IN p_FROM_YMD CHAR(8),
    IN p_TO_YMD CHAR(8),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_tbl INT DEFAULT 0;

    SELECT COUNT(*) INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_nvl_basis';
    IF IFNULL(v_tbl, 0) = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Thieu bang production_cost_nvl_basis';
    END IF;

    DELETE FROM production_cost_nvl_basis_line
    WHERE RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND COMPANY_CD = p_COMPANY_CD;
    DELETE FROM production_cost_nvl_basis
    WHERE RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND COMPANY_CD = p_COMPANY_CD;
    DELETE FROM production_cost_nvl_orphan
    WHERE RUN_ID = p_RUN_ID AND RUN_VERSION = p_RUN_VERSION AND COMPANY_CD = p_COMPANY_CD;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_raw;
    CREATE TEMPORARY TABLE tmp_pc_nvl_raw (
        PRODUCT_ID BIGINT NOT NULL,
        INVENTORY_ID BIGINT NOT NULL,
        FINISHED_LINE_ID BIGINT NOT NULL,
        BOM_MATERIAL_ID BIGINT NULL,
        OUTPUT_ID BIGINT NOT NULL,
        CHITDETAIL_ID BIGINT NULL,
        MATERIAL_PRODUCT_ID BIGINT NULL,
        CONFIRMED_QUANTITY DECIMAL(18,6) NOT NULL DEFAULT 0,
        OUTPUT_AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        DETAIL_AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0,
        KEY idx_tmp_nvl_out (OUTPUT_ID),
        KEY idx_tmp_nvl_det (CHITDETAIL_ID),
        KEY idx_tmp_nvl_prod (PRODUCT_ID)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    -- 1) Candidate lines linked by BOM + period filter on chit YMD
    INSERT INTO tmp_pc_nvl_raw (
        PRODUCT_ID, INVENTORY_ID, FINISHED_LINE_ID, BOM_MATERIAL_ID, OUTPUT_ID,
        CHITDETAIL_ID, MATERIAL_PRODUCT_ID, CONFIRMED_QUANTITY, OUTPUT_AMOUNT, DETAIL_AMOUNT
    )
    SELECT
        f.FINISHED_PRODUCT_ID,
        f.INVENTORY_ID,
        f.FINISHED_LINE_ID,
        m.BOM_MATERIAL_ID,
        m.OUTPUT_ID,
        o.CHITDETAIL_ID,
        m.MATERIAL_PRODUCT_ID,
        IFNULL(m.CONFIRMED_QUANTITY, 0),
        ROUND(IFNULL(o.AMOUNT_CC, 0), 2),
        ROUND(IFNULL(d.AMOUNT, 0), 2)
    FROM chit_inventory_bom_finished f
    INNER JOIN chit_inventory_bom_material m
        ON m.COMPANY_CD = f.COMPANY_CD
       AND m.FINISHED_LINE_ID = f.FINISHED_LINE_ID
       AND m.ISDEL = '0'
    INNER JOIN chit_inventory_output o
        ON o.COMPANY_CD = m.COMPANY_CD
       AND o.OUTPUT_ID = m.OUTPUT_ID
       AND o.ISDEL = '0'
    INNER JOIN chitdetailinfo d
        ON d.COMPANY_CD = o.COMPANY_CD
       AND d.CHITDETAIL_ID = o.CHITDETAIL_ID
       AND d.ISDEL = '0'
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = d.COMPANY_CD
       AND c.CHIT_ID = d.CHIT_ID
       AND c.ISDEL = '0'
    WHERE f.COMPANY_CD = p_COMPANY_CD
      AND f.ISDEL = '0'
      AND IFNULL(m.OUTPUT_ID, 0) > 0
      AND c.CHIT_YMD >= p_FROM_YMD
      AND c.CHIT_YMD <= p_TO_YMD
      AND TRIM(d.DEBIT) LIKE '621%'
      AND TRIM(d.CREDIT) LIKE '152%'
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK';

    -- 2) Orphans: output N621/C152 in period without BOM material link
    INSERT INTO production_cost_nvl_orphan (
        RUN_ID, RUN_VERSION, COMPANY_CD, OUTPUT_ID, CHITDETAIL_ID, INVENTORY_ID,
        MATERIAL_PRODUCT_ID, AMOUNT, REASON_CODE, WARNING_TEXT, ISDEL, CREATE_AT
    )
    SELECT
        p_RUN_ID, p_RUN_VERSION, p_COMPANY_CD,
        o.OUTPUT_ID, o.CHITDETAIL_ID, o.INVENTORY_ID, o.PRODUCT_ID,
        ROUND(IFNULL(d.AMOUNT, 0), 2),
        'NO_BOM',
        'Xuat NVL N621/C152 khong gan BOM finished — loai khoi AMOUNT_QUANTITY',
        '0', NOW()
    FROM chit_inventory_output o
    INNER JOIN chitdetailinfo d
        ON d.COMPANY_CD = o.COMPANY_CD
       AND d.CHITDETAIL_ID = o.CHITDETAIL_ID
       AND d.ISDEL = '0'
    INNER JOIN chitinfo c
        ON c.COMPANY_CD = d.COMPANY_CD
       AND c.CHIT_ID = d.CHIT_ID
       AND c.ISDEL = '0'
    LEFT JOIN chit_inventory_bom_material m
        ON m.COMPANY_CD = o.COMPANY_CD
       AND m.OUTPUT_ID = o.OUTPUT_ID
       AND m.ISDEL = '0'
    WHERE o.COMPANY_CD = p_COMPANY_CD
      AND o.ISDEL = '0'
      AND c.CHIT_YMD >= p_FROM_YMD
      AND c.CHIT_YMD <= p_TO_YMD
      AND TRIM(d.DEBIT) LIKE '621%'
      AND TRIM(d.CREDIT) LIKE '152%'
      AND IFNULL(c.INPUT_TYPE, '') <> 'LOCK'
      AND m.BOM_MATERIAL_ID IS NULL;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_out_share;
    CREATE TEMPORARY TABLE tmp_pc_nvl_out_share (
        OUTPUT_ID BIGINT NOT NULL PRIMARY KEY,
        QTY_SUM DECIMAL(18,6) NOT NULL DEFAULT 0,
        ROW_CNT INT NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_pc_nvl_out_share (OUTPUT_ID, QTY_SUM, ROW_CNT)
    SELECT OUTPUT_ID, SUM(CONFIRMED_QUANTITY), COUNT(*)
    FROM tmp_pc_nvl_raw
    GROUP BY OUTPUT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_det_share;
    CREATE TEMPORARY TABLE tmp_pc_nvl_det_share (
        CHITDETAIL_ID BIGINT NOT NULL PRIMARY KEY,
        OUT_AMT_SUM DECIMAL(18,2) NOT NULL DEFAULT 0,
        DETAIL_AMOUNT DECIMAL(18,2) NOT NULL DEFAULT 0
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

    INSERT INTO tmp_pc_nvl_det_share (CHITDETAIL_ID, OUT_AMT_SUM, DETAIL_AMOUNT)
    SELECT
        r.CHITDETAIL_ID,
        SUM(DISTINCT ROUND(r.OUTPUT_AMOUNT, 2)),
        MAX(r.DETAIL_AMOUNT)
    FROM tmp_pc_nvl_raw r
    WHERE IFNULL(r.CHITDETAIL_ID, 0) > 0
    GROUP BY r.CHITDETAIL_ID;

    -- NOTE: SUM(DISTINCT OUTPUT_AMOUNT) weak if two outputs same amount — use subquery by OUTPUT
    DELETE FROM tmp_pc_nvl_det_share;
    INSERT INTO tmp_pc_nvl_det_share (CHITDETAIL_ID, OUT_AMT_SUM, DETAIL_AMOUNT)
    SELECT
        x.CHITDETAIL_ID,
        SUM(x.OUTPUT_AMOUNT),
        MAX(x.DETAIL_AMOUNT)
    FROM (
        SELECT CHITDETAIL_ID, OUTPUT_ID, MAX(OUTPUT_AMOUNT) AS OUTPUT_AMOUNT, MAX(DETAIL_AMOUNT) AS DETAIL_AMOUNT
        FROM tmp_pc_nvl_raw
        WHERE IFNULL(CHITDETAIL_ID, 0) > 0
        GROUP BY CHITDETAIL_ID, OUTPUT_ID
    ) x
    GROUP BY x.CHITDETAIL_ID;

    -- 3) Allocate amount per material row (không correlated subquery — MySQL 5.6 temp table)
    -- SOURCE_AMOUNT = phần DETAIL_AMOUNT thuộc OUTPUT (chia theo AMOUNT_CC các OUTPUT cùng CHITDETAIL)
    -- ALLOCATED_AMOUNT = SOURCE_AMOUNT × (qty / Σqty cùng OUTPUT) khi 1 OUTPUT → nhiều TP
    INSERT INTO production_cost_nvl_basis_line (
        RUN_ID, RUN_VERSION, COMPANY_CD, PRODUCT_ID, INVENTORY_ID, FINISHED_LINE_ID,
        BOM_MATERIAL_ID, OUTPUT_ID, CHITDETAIL_ID, MATERIAL_PRODUCT_ID,
        CONFIRMED_QUANTITY, SOURCE_AMOUNT, ALLOCATED_AMOUNT, SPLIT_RATIO,
        ISDEL, CREATE_BY, CREATE_AT
    )
    SELECT
        p_RUN_ID,
        p_RUN_VERSION,
        p_COMPANY_CD,
        r.PRODUCT_ID,
        r.INVENTORY_ID,
        r.FINISHED_LINE_ID,
        r.BOM_MATERIAL_ID,
        r.OUTPUT_ID,
        r.CHITDETAIL_ID,
        r.MATERIAL_PRODUCT_ID,
        r.CONFIRMED_QUANTITY,
        ROUND(
            CASE
                WHEN IFNULL(ds.OUT_AMT_SUM, 0) > 0 THEN r.DETAIL_AMOUNT * (r.OUTPUT_AMOUNT / ds.OUT_AMT_SUM)
                ELSE r.DETAIL_AMOUNT
            END
        , 2) AS SOURCE_AMOUNT,
        ROUND(
            CASE
                WHEN IFNULL(os.QTY_SUM, 0) > 0 THEN
                    (CASE
                        WHEN IFNULL(ds.OUT_AMT_SUM, 0) > 0 THEN r.DETAIL_AMOUNT * (r.OUTPUT_AMOUNT / ds.OUT_AMT_SUM)
                        ELSE r.DETAIL_AMOUNT
                    END) * (r.CONFIRMED_QUANTITY / os.QTY_SUM)
                WHEN IFNULL(os.ROW_CNT, 0) > 1 THEN
                    (CASE
                        WHEN IFNULL(ds.OUT_AMT_SUM, 0) > 0 THEN r.DETAIL_AMOUNT * (r.OUTPUT_AMOUNT / ds.OUT_AMT_SUM)
                        ELSE r.DETAIL_AMOUNT
                    END) / os.ROW_CNT
                ELSE
                    CASE
                        WHEN IFNULL(ds.OUT_AMT_SUM, 0) > 0 THEN r.DETAIL_AMOUNT * (r.OUTPUT_AMOUNT / ds.OUT_AMT_SUM)
                        ELSE r.DETAIL_AMOUNT
                    END
            END
        , 2) AS ALLOCATED_AMOUNT,
        CASE
            WHEN IFNULL(os.QTY_SUM, 0) > 0 THEN r.CONFIRMED_QUANTITY / os.QTY_SUM
            WHEN IFNULL(os.ROW_CNT, 0) > 1 THEN 1 / os.ROW_CNT
            ELSE 1
        END AS SPLIT_RATIO,
        '0',
        p_USER_ID,
        NOW()
    FROM tmp_pc_nvl_raw r
    LEFT JOIN tmp_pc_nvl_out_share os ON os.OUTPUT_ID = r.OUTPUT_ID
    LEFT JOIN tmp_pc_nvl_det_share ds ON ds.CHITDETAIL_ID = r.CHITDETAIL_ID;

    -- Orphan split qty zero when multiple finished share output but qty all 0 already handled by ROW_CNT equal split

    INSERT INTO production_cost_nvl_basis (
        RUN_ID, RUN_VERSION, COMPANY_CD, PRODUCT_ID, AMOUNT_QUANTITY, LINE_COUNT,
        ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
    )
    SELECT
        p_RUN_ID,
        p_RUN_VERSION,
        p_COMPANY_CD,
        PRODUCT_ID,
        ROUND(SUM(ALLOCATED_AMOUNT), 0),
        COUNT(*),
        '0',
        p_USER_ID,
        NOW(),
        p_USER_ID,
        NOW()
    FROM production_cost_nvl_basis_line
    WHERE RUN_ID = p_RUN_ID
      AND RUN_VERSION = p_RUN_VERSION
      AND COMPANY_CD = p_COMPANY_CD
      AND ISDEL = '0'
    GROUP BY PRODUCT_ID;

    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_raw;
    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_out_share;
    DROP TEMPORARY TABLE IF EXISTS tmp_pc_nvl_det_share;
END proc$$

DELIMITER ;
