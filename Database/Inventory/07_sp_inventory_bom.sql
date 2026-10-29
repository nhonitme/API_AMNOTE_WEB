-- Inventory BOM allocation snapshot stored procedures
-- Deploy to each COMPANY database (MySQL 5.6+) after 05_inventory_bom_snapshot.sql
-- p_PRODUCT_IDS: comma-separated PRODUCT_ID list (e.g. '11,22,33')

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_finished_get`$$
CREATE PROCEDURE `sp_inventory_bom_finished_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INVENTORY_ID` BIGINT
)
BEGIN
    SELECT
        f.FINISHED_LINE_ID,
        f.FINISHED_PRODUCT_ID,
        f.FINISHED_QUANTITY,
        f.SORT,
        p.PRODUCT_CD,
        p.PRODUCT_NM_VIET,
        u.UNIT_CD
    FROM chit_inventory_bom_finished f
    LEFT JOIN product_info p
        ON p.PRODUCT_ID = f.FINISHED_PRODUCT_ID
       AND p.COMPANY_CD = f.COMPANY_CD
    LEFT JOIN product_unit u
        ON u.UNIT_ID = p.UNIT_ID
       AND u.COMPANY_CD = f.COMPANY_CD
    WHERE f.COMPANY_CD = p_COMPANY_CD
      AND f.INVENTORY_ID = p_INVENTORY_ID
      AND f.ISDEL = '0'
    ORDER BY f.SORT, f.FINISHED_LINE_ID;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_material_get`$$
CREATE PROCEDURE `sp_inventory_bom_material_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INVENTORY_ID` BIGINT
)
BEGIN
    SELECT
        m.BOM_MATERIAL_ID,
        m.FINISHED_LINE_ID,
        m.MATERIAL_PRODUCT_ID,
        m.STORE_ID,
        m.BOM_QUANTITY,
        m.CALCULATED_QUANTITY,
        m.CONFIRMED_QUANTITY,
        m.OUTPUT_ID,
        m.SORT,
        p.PRODUCT_CD AS MATERIAL_PRODUCT_CD,
        p.PRODUCT_NM_VIET AS MATERIAL_PRODUCT_NM,
        u.UNIT_CD
    FROM chit_inventory_bom_material m
    INNER JOIN chit_inventory_bom_finished f
        ON f.FINISHED_LINE_ID = m.FINISHED_LINE_ID
       AND f.COMPANY_CD = m.COMPANY_CD
       AND f.INVENTORY_ID = m.INVENTORY_ID
       AND f.ISDEL = '0'
    LEFT JOIN product_info p
        ON p.PRODUCT_ID = m.MATERIAL_PRODUCT_ID
       AND p.COMPANY_CD = m.COMPANY_CD
    LEFT JOIN product_unit u
        ON u.UNIT_ID = p.UNIT_ID
       AND u.COMPANY_CD = m.COMPANY_CD
    WHERE m.COMPANY_CD = p_COMPANY_CD
      AND m.INVENTORY_ID = p_INVENTORY_ID
      AND m.ISDEL = '0'
    ORDER BY m.SORT, m.BOM_MATERIAL_ID;
END$$


DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_latest_input_stores_get`$$

CREATE PROCEDURE `sp_inventory_bom_latest_input_stores_get`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PRODUCT_IDS TEXT
)
BEGIN
    DECLARE v_PRODUCT_IDS TEXT;

    SET v_PRODUCT_IDS =
        REPLACE(IFNULL(p_PRODUCT_IDS, ''), ' ', '');

    SELECT
        p.PRODUCT_ID,

        COALESCE(i.STORE_ID, p.STORE_ID) AS STORE_ID,

        COALESCE(
            s_in.STORE_CD,
            s.STORE_CD
        ) AS STORE_CD,

        COALESCE(
            s_in.STORE_NM_VIET,
            s.STORE_NM_VIET
        ) AS STORE_NM_VIET

    FROM product_info p

    LEFT JOIN chit_inventory_input i
        ON i.COMPANY_CD = p.COMPANY_CD
        AND i.PRODUCT_ID = p.PRODUCT_ID
        AND i.INPUT_ID = (
            SELECT x.INPUT_ID
            FROM chit_inventory_input x
            WHERE x.COMPANY_CD = p.COMPANY_CD
              AND x.PRODUCT_ID = p.PRODUCT_ID
              AND (
                  x.ISDEL IS NULL
                  OR x.ISDEL <> '1'
              )
              AND x.STORE_ID > 0
              AND x.INVENTORY_YMD IS NOT NULL
            ORDER BY
                x.INVENTORY_YMD DESC,
                x.INPUT_ID DESC
            LIMIT 1
        )

    LEFT JOIN store_info s_in
        ON s_in.COMPANY_CD = i.COMPANY_CD
        AND s_in.STORE_ID = i.STORE_ID
        AND IFNULL(s_in.ISDEL, '0') = '0'

    LEFT JOIN store_info s
        ON s.COMPANY_CD = p.COMPANY_CD
        AND s.STORE_ID = p.STORE_ID
        AND IFNULL(s.ISDEL, '0') = '0'

    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(p.ISDEL, '0') = '0'
      AND FIND_IN_SET(
          p.PRODUCT_ID,
          v_PRODUCT_IDS
      ) > 0
      AND COALESCE(
          i.STORE_ID,
          p.STORE_ID,
          0
      ) > 0;

END$$

DELIMITER ;


DROP PROCEDURE IF EXISTS `sp_inventory_bom_products_validate`$$
CREATE PROCEDURE `sp_inventory_bom_products_validate`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_PRODUCT_IDS` TEXT
)
BEGIN
    SELECT PRODUCT_ID
    FROM product_info
    WHERE COMPANY_CD = p_COMPANY_CD
      AND IFNULL(ISDEL, '0') = '0'
      AND FIND_IN_SET(PRODUCT_ID, REPLACE(IFNULL(p_PRODUCT_IDS, ''), ' ', '')) > 0;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_soft_delete`$$
CREATE PROCEDURE `sp_inventory_bom_soft_delete`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INVENTORY_ID` BIGINT,
    IN `p_UPDATE_BY` VARCHAR(20)
)
BEGIN
    UPDATE chit_inventory_bom_material
       SET ISDEL = '1',
           UPDATE_BY = p_UPDATE_BY
     WHERE COMPANY_CD = p_COMPANY_CD
       AND INVENTORY_ID = p_INVENTORY_ID
       AND ISDEL = '0';

    UPDATE chit_inventory_bom_finished
       SET ISDEL = '1',
           UPDATE_BY = p_UPDATE_BY
     WHERE COMPANY_CD = p_COMPANY_CD
       AND INVENTORY_ID = p_INVENTORY_ID
       AND ISDEL = '0';
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_finished_insert`$$
CREATE PROCEDURE `sp_inventory_bom_finished_insert`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INVENTORY_ID` BIGINT,
    IN `p_FINISHED_PRODUCT_ID` BIGINT,
    IN `p_FINISHED_QUANTITY` DECIMAL(24,6),
    IN `p_SORT` INT,
    IN `p_USER_ID` VARCHAR(20)
)
BEGIN
    INSERT INTO chit_inventory_bom_finished (
        COMPANY_CD,
        INVENTORY_ID,
        FINISHED_PRODUCT_ID,
        FINISHED_QUANTITY,
        SORT,
        CREATE_BY,
        UPDATE_BY
    ) VALUES (
        p_COMPANY_CD,
        p_INVENTORY_ID,
        p_FINISHED_PRODUCT_ID,
        p_FINISHED_QUANTITY,
        p_SORT,
        p_USER_ID,
        p_USER_ID
    );

    SELECT LAST_INSERT_ID() AS ID;
END$$

DROP PROCEDURE IF EXISTS `sp_inventory_bom_material_insert`$$
CREATE PROCEDURE `sp_inventory_bom_material_insert`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_INVENTORY_ID` BIGINT,
    IN `p_FINISHED_LINE_ID` BIGINT,
    IN `p_MATERIAL_PRODUCT_ID` BIGINT,
    IN `p_STORE_ID` BIGINT,
    IN `p_BOM_QUANTITY` DECIMAL(24,10),
    IN `p_CALCULATED_QUANTITY` DECIMAL(24,6),
    IN `p_CONFIRMED_QUANTITY` DECIMAL(24,6),
    IN `p_OUTPUT_ID` BIGINT,
    IN `p_SORT` INT,
    IN `p_USER_ID` VARCHAR(20)
)
BEGIN
    INSERT INTO chit_inventory_bom_material (
        COMPANY_CD,
        INVENTORY_ID,
        FINISHED_LINE_ID,
        MATERIAL_PRODUCT_ID,
        STORE_ID,
        BOM_QUANTITY,
        CALCULATED_QUANTITY,
        CONFIRMED_QUANTITY,
        OUTPUT_ID,
        SORT,
        CREATE_BY,
        UPDATE_BY
    ) VALUES (
        p_COMPANY_CD,
        p_INVENTORY_ID,
        p_FINISHED_LINE_ID,
        p_MATERIAL_PRODUCT_ID,
        p_STORE_ID,
        p_BOM_QUANTITY,
        p_CALCULATED_QUANTITY,
        p_CONFIRMED_QUANTITY,
        p_OUTPUT_ID,
        p_SORT,
        p_USER_ID,
        p_USER_ID
    );
END$$

DELIMITER ;
