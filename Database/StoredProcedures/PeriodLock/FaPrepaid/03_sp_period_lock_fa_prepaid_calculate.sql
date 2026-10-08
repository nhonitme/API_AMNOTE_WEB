DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_calculate`$$

CREATE PROCEDURE `sp_period_lock_fa_prepaid_calculate`(
    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_PERIOD_YM CHAR(6) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_USER_ID VARCHAR(50) CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_now DATETIME;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET v_now = NOW();

    START TRANSACTION;

    -- Dọn dữ liệu chạy dở (active, chưa tạo CT) hoặc đã mở khóa (CANCEL_YN='Y').
    -- uk_fa_asset_depre_posted_01 = (COMPANY_CD, ASSET_ID, DEPRE_YM) — bản ghi hủy vẫn chặn INSERT mới.
    DELETE pa
    FROM fa_asset_depre_posted_alloc pa
    INNER JOIN fa_asset_depre_posted p
        ON p.POST_ID = pa.POST_ID
       AND p.COMPANY_CD = pa.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND (
          IFNULL(p.CANCEL_YN, 'N') = 'Y'
          OR (
              IFNULL(p.CANCEL_YN, 'N') = 'N'
              AND p.CHITINFO_ID IS NULL
          )
      );

    DELETE
    FROM fa_asset_depre_posted
    WHERE COMPANY_CD = p_COMPANY_CD
      AND DEPRE_YM = p_PERIOD_YM
      AND (
          IFNULL(CANCEL_YN, 'N') = 'Y'
          OR (
              IFNULL(CANCEL_YN, 'N') = 'N'
              AND CHITINFO_ID IS NULL
          )
      );

    -- Validate sau khi dọn dữ liệu chạy dở
    CALL sp_period_lock_fa_prepaid_validate(
        p_COMPANY_CD,
        p_PERIOD_YM,
        p_USER_ID
    );

    INSERT INTO fa_asset_depre_posted (
        COMPANY_CD,
        ASSET_ID,
        DEPRE_YM,
        DEPRE_TYPE,
        DEPRE_AMT,
        ACCUM_DEPRE_AMT,
        END_BOOK_AMT,
        CHITINFO_ID,
        CHIT_NO,
        LOCK_YN,
        CANCEL_YN,
        CREATE_DT,
        CREATE_BY
    )
    SELECT
        x.COMPANY_CD,
        x.ASSET_ID,
        x.DEPRE_YM,
        x.DEPRE_TYPE,
        x.DEPRE_AMT,
        x.ACCUM_DEPRE_AMT,
        x.END_BOOK_AMT,
        NULL AS CHITINFO_ID,
        NULL AS CHIT_NO,
        'Y' AS LOCK_YN,
        'N' AS CANCEL_YN,
        v_now AS CREATE_DT,
        p_USER_ID AS CREATE_BY
    FROM (
        SELECT
            a.COMPANY_CD,
            a.ASSET_ID,
            p_PERIOD_YM AS DEPRE_YM,

            CASE
                WHEN p_PERIOD_YM = a.DEPRE_START_YM THEN 'FIRST'
                WHEN p_PERIOD_YM = a.DEPRE_END_YM THEN 'LAST'
                ELSE 'NORMAL'
            END AS DEPRE_TYPE,

            CASE
                WHEN p_PERIOD_YM = a.DEPRE_START_YM THEN IFNULL(a.FIRST_DEPRE_AMT, 0)
                WHEN p_PERIOD_YM = a.DEPRE_END_YM THEN IFNULL(a.LAST_DEPRE_AMT, 0)
                ELSE IFNULL(a.NORMAL_DEPRE_AMT, 0)
            END AS DEPRE_AMT,

            IFNULL(a.ACCUM_DEPRE_AMT, 0)
            + IFNULL(prev.PREV_SUM, 0)
            + CASE
                WHEN p_PERIOD_YM = a.DEPRE_START_YM THEN IFNULL(a.FIRST_DEPRE_AMT, 0)
                WHEN p_PERIOD_YM = a.DEPRE_END_YM THEN IFNULL(a.LAST_DEPRE_AMT, 0)
                ELSE IFNULL(a.NORMAL_DEPRE_AMT, 0)
              END AS ACCUM_DEPRE_AMT,

            IFNULL(a.ORIGINAL_AMT, 0)
            - (
                IFNULL(a.ACCUM_DEPRE_AMT, 0)
                + IFNULL(prev.PREV_SUM, 0)
                + CASE
                    WHEN p_PERIOD_YM = a.DEPRE_START_YM THEN IFNULL(a.FIRST_DEPRE_AMT, 0)
                    WHEN p_PERIOD_YM = a.DEPRE_END_YM THEN IFNULL(a.LAST_DEPRE_AMT, 0)
                    ELSE IFNULL(a.NORMAL_DEPRE_AMT, 0)
                  END
              ) AS END_BOOK_AMT

        FROM fa_asset a
        LEFT JOIN (
            SELECT
                p.ASSET_ID,
                SUM(IFNULL(p.DEPRE_AMT, 0)) AS PREV_SUM
            FROM fa_asset_depre_posted p
            WHERE p.COMPANY_CD = p_COMPANY_CD
              AND p.DEPRE_YM < p_PERIOD_YM
              AND IFNULL(p.CANCEL_YN, 'N') = 'N'
            GROUP BY p.ASSET_ID
        ) prev ON prev.ASSET_ID = a.ASSET_ID
        WHERE a.COMPANY_CD = p_COMPANY_CD
          AND IFNULL(a.ISDEL, '0') = '0'
          AND a.STATUS = 'IN_USE'
          AND p_PERIOD_YM BETWEEN a.DEPRE_START_YM AND a.DEPRE_END_YM
          AND NOT EXISTS (
              SELECT 1
              FROM fa_asset_depre_posted p2
              WHERE p2.COMPANY_CD = a.COMPANY_CD
                AND p2.ASSET_ID = a.ASSET_ID
                AND p2.DEPRE_YM = p_PERIOD_YM
                AND IFNULL(p2.CANCEL_YN, 'N') = 'N'
          )
    ) x
    WHERE x.DEPRE_AMT >= 0
      AND x.END_BOOK_AMT >= -0.01;

    INSERT INTO fa_asset_depre_posted_alloc (
        POST_ID,
        COMPANY_CD,
        ASSET_ID,
        DEPRE_YM,
        ALLOC_ID,
        ALLOC_SEQ,
        DEBIT_ACCT_CD,
        CREDIT_ACCT_CD,
        DEPARTMENT_ID,
        ALLOC_TYPE,
        ALLOC_RATE,
        DEPRE_AMT,
        CHITDETAIL_ID,
        CREATE_DT,
        CREATE_BY
    )
    SELECT
        p.POST_ID,
        p.COMPANY_CD,
        p.ASSET_ID,
        p.DEPRE_YM,
        al.ALLOC_ID,
        al.ALLOC_SEQ,
        al.DEBIT_ACCT_CD,
        al.CREDIT_ACCT_CD,
        al.DEPARTMENT_ID,
        al.ALLOC_TYPE,
        al.ALLOC_RATE,
        CASE p.DEPRE_TYPE
            WHEN 'FIRST' THEN IFNULL(al.FIRST_ALLOC_AMT, 0)
            WHEN 'NORMAL' THEN IFNULL(al.NORMAL_ALLOC_AMT, 0)
            WHEN 'LAST' THEN IFNULL(al.LAST_ALLOC_AMT, 0)
            ELSE 0
        END AS DEPRE_AMT,
        NULL AS CHITDETAIL_ID,
        v_now AS CREATE_DT,
        p_USER_ID AS CREATE_BY
    FROM fa_asset_depre_posted p
    INNER JOIN fa_asset_depre_alloc al
        ON al.COMPANY_CD = p.COMPANY_CD
       AND al.ASSET_ID = p.ASSET_ID
       AND IFNULL(al.ISDEL, '0') = '0'
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NULL
      AND CASE p.DEPRE_TYPE
            WHEN 'FIRST' THEN IFNULL(al.FIRST_ALLOC_AMT, 0)
            WHEN 'NORMAL' THEN IFNULL(al.NORMAL_ALLOC_AMT, 0)
            WHEN 'LAST' THEN IFNULL(al.LAST_ALLOC_AMT, 0)
            ELSE 0
          END <> 0;

    COMMIT;
END$$

DELIMITER ;