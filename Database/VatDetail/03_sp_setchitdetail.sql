-- =============================================================================
-- VAT Detail: update setchitdetail — persist VAT_DIRECTION / rate / customs
-- Target: company database
-- Safe to re-run (DROP + CREATE).
-- Do NOT run on production without review.
-- =============================================================================

DROP PROCEDURE IF EXISTS `setchitdetail`;
DELIMITER //
CREATE PROCEDURE `setchitdetail`(
    IN p_CHITDETAIL_ID BIGINT,
    IN p_COMPANY_CD VARCHAR(20),
    IN p_INPUT_TYPE VARCHAR(20),
    IN p_CHIT_ID BIGINT,
    IN p_CHITDETAIL_CD VARCHAR(20),
    IN p_CHIT_YMD VARCHAR(8),
    IN p_CHIT_VMD VARCHAR(8),
    IN p_DEBIT VARCHAR(20),
    IN p_CREDIT VARCHAR(20),
    IN p_AMOUNT DECIMAL(18,2),
    IN p_FC_AMOUNT DECIMAL(18,2),
    IN p_FC_TYPE VARCHAR(10),
    IN p_FC_RATE DECIMAL(18,6),
    IN p_FC_DATETIME DATETIME,
    IN p_SORT INT,
    IN p_CHITDETAIL_VAT_CD VARCHAR(20),
    IN p_MG_CD VARCHAR(20),
    IN p_MG_CD_2 VARCHAR(20),
    IN p_MR_CD VARCHAR(20),
    IN p_MR_CD2 VARCHAR(20),
    IN p_BANK_ID BIGINT,
    IN p_BANK_CD VARCHAR(20),
    IN p_BANK_OWN_CD VARCHAR(20),
    IN p_CUSTOMER_ID BIGINT,
    IN p_CUSTOMER_CD VARCHAR(20),
    IN p_CUSTOMER_OWN_CD VARCHAR(20),
    IN p_DEPARTMENT_ID BIGINT,
    IN p_DEPARTMENT_CD VARCHAR(20),
    IN p_DEPARTMENT_CD_2 VARCHAR(20),
    IN p_HASINVENTORY CHAR(1),
    IN p_INVENTORY_YMD VARCHAR(8),
    IN p_ISPAY CHAR(1),
    IN p_ISCOLLECT CHAR(1),
    IN p_VAT_SERIAL_NO VARCHAR(50),
    IN p_VAT_CHIT_NO VARCHAR(50),
    IN p_VAT_CHIT_NO_2 VARCHAR(50),
    IN p_VAT_AMOUNT DECIMAL(18,2),
    IN p_VAT_TAXABLE_AMOUNT DECIMAL(18,2),
    IN p_FO_VAT_AMOUNT DECIMAL(18,2),
    IN p_VAT_ISFREE CHAR(1),
    IN p_VAT_INVOICE_CD VARCHAR(50),
    IN p_VAT_INVOICE_NM VARCHAR(255),
    IN p_VAT_INFO_TYPE VARCHAR(50),
    IN p_VAT_COMPANY_ISSUE VARCHAR(255),
    IN p_VAT_COMPANY_ISSUE_ADDRESS VARCHAR(500),
    IN p_VAT_COMPANY_ISSUE_CD VARCHAR(50),
    IN p_VAT_COMPANY_TAXCD VARCHAR(50),
    IN p_VAT_PRODUCT_NM VARCHAR(255),
    IN p_VAT_ETC VARCHAR(500),
    IN p_VAT_YMD VARCHAR(8),
    IN p_VAT_YMD_2 VARCHAR(8),
    IN p_VAT_INQUIRY_IN VARCHAR(50),
    IN p_VAT_INQUIRY_CODE VARCHAR(50),
    IN p_IS_NEXTVAT CHAR(1),
    IN p_VAT_DIRECTION VARCHAR(4),
    IN p_VAT_RATE_CD VARCHAR(20),
    IN p_VAT_RATE_PERCENT DECIMAL(9,4),
    IN p_VAT_CUSTOMS_EXCHANGE_RATE DECIMAL(20,8),
    IN p_UNDEFINE VARCHAR(255),
    IN p_DETAIL_DESCRIPTION_VIET VARCHAR(1000),
    IN p_DETAIL_DESCRIPTION_ENG VARCHAR(1000),
    IN p_DETAIL_DESCRIPTION_KOR VARCHAR(1000),
    IN p_UPDATE_BY VARCHAR(50)
)
BEGIN
    DECLARE v_chitdetail_id BIGINT DEFAULT 0;
    DECLARE v_chit_cd VARCHAR(20);
    DECLARE v_bank_id BIGINT DEFAULT NULL;
    DECLARE v_bank_cd VARCHAR(20);
    DECLARE v_customer_id BIGINT DEFAULT NULL;
    DECLARE v_customer_cd VARCHAR(20);
    DECLARE v_customer_cd_input VARCHAR(20);
    DECLARE v_department_id BIGINT DEFAULT NULL;
    DECLARE v_department_cd VARCHAR(20);

    SELECT h.CHIT_CD INTO v_chit_cd
    FROM chitinfo h
    WHERE h.COMPANY_CD = p_COMPANY_CD
      AND h.CHIT_ID = p_CHIT_ID
      AND IFNULL(h.INPUT_TYPE, '') = IFNULL(p_INPUT_TYPE, '')
      AND IFNULL(h.ISDEL, '0') = '0'
    LIMIT 1;

    SET v_customer_id = NULLIF(IFNULL(p_CUSTOMER_ID, 0), 0);
    SET v_customer_cd_input = NULLIF(TRIM(p_CUSTOMER_CD), '');
    SET v_customer_cd = v_customer_cd_input;

    IF v_customer_id IS NOT NULL THEN
        SET v_customer_cd = NULL;
        SELECT customer.CUSTOMER_CD INTO v_customer_cd
        FROM customer_info customer
        WHERE customer.COMPANY_CD = p_COMPANY_CD
          AND customer.CUSTOMER_ID = v_customer_id
          AND IFNULL(customer.ISDEL, '0') = '0'
        LIMIT 1;

        IF v_customer_cd IS NULL THEN
            SET v_customer_id = NULL;
            SET v_customer_cd = v_customer_cd_input;
        END IF;
    END IF;

    IF v_customer_id IS NULL AND v_customer_cd IS NOT NULL THEN
        SELECT customer.CUSTOMER_ID INTO v_customer_id
        FROM customer_info customer
        WHERE customer.COMPANY_CD = p_COMPANY_CD
          AND customer.CUSTOMER_CD = v_customer_cd
          AND IFNULL(customer.ISDEL, '0') = '0'
        LIMIT 1;
    END IF;

    SET v_bank_id = NULLIF(IFNULL(p_BANK_ID, 0), 0);
    SET v_bank_cd = NULLIF(TRIM(p_BANK_CD), '');

    IF v_bank_id IS NOT NULL THEN
        SET v_bank_cd = NULL;
        SELECT bank.BANK_CD INTO v_bank_cd
        FROM bank_info bank
        WHERE bank.COMPANY_CD = p_COMPANY_CD
          AND bank.BANK_ID = v_bank_id
          AND IFNULL(bank.ISDEL, '0') = '0'
        LIMIT 1;

        IF v_bank_cd IS NULL THEN
            SET v_bank_id = NULL;
            SET v_bank_cd = NULLIF(TRIM(p_BANK_CD), '');
        END IF;
    END IF;

    IF v_bank_id IS NULL AND v_bank_cd IS NOT NULL THEN
        SELECT bank.BANK_ID INTO v_bank_id
        FROM bank_info bank
        WHERE bank.COMPANY_CD = p_COMPANY_CD
          AND bank.BANK_CD = v_bank_cd
          AND IFNULL(bank.ISDEL, '0') = '0'
        LIMIT 1;
    END IF;

    SET v_department_id = NULLIF(IFNULL(p_DEPARTMENT_ID, 0), 0);
    SET v_department_cd = NULLIF(TRIM(p_DEPARTMENT_CD), '');

    IF v_department_id IS NOT NULL THEN
        SET v_department_cd = NULL;
        SELECT department.DEPARTMENT_CD INTO v_department_cd
        FROM department_info department
        WHERE department.COMPANY_CD = p_COMPANY_CD
          AND department.DEPARTMENT_ID = v_department_id
          AND IFNULL(department.ISDEL, '0') = '0'
        LIMIT 1;

        IF v_department_cd IS NULL THEN
            SET v_department_id = NULL;
            SET v_department_cd = NULLIF(TRIM(p_DEPARTMENT_CD), '');
        END IF;
    END IF;

    IF v_department_id IS NULL AND v_department_cd IS NOT NULL THEN
        SELECT department.DEPARTMENT_ID INTO v_department_id
        FROM department_info department
        WHERE department.COMPANY_CD = p_COMPANY_CD
          AND department.DEPARTMENT_CD = v_department_cd
          AND IFNULL(department.ISDEL, '0') = '0'
        LIMIT 1;
    END IF;

    IF IFNULL(p_CHITDETAIL_ID, 0) = 0 THEN
        INSERT INTO chitdetailinfo (COMPANY_CD, CHIT_ID, CHIT_CD, CHITDETAIL_CD, CHIT_YMD, DEBIT, CREDIT, AMOUNT, FC_AMOUNT, FC_TYPE, FC_RATE, FC_DATETIME, SORT, ISDEL, CREATE_BY, UPDATE_BY)
        VALUES (p_COMPANY_CD, p_CHIT_ID, v_chit_cd, p_CHITDETAIL_CD, p_CHIT_YMD, p_DEBIT, p_CREDIT, IFNULL(p_AMOUNT, 0), IFNULL(p_FC_AMOUNT, 0), p_FC_TYPE, IFNULL(p_FC_RATE, 0), p_FC_DATETIME, IFNULL(p_SORT, 0), '0', p_UPDATE_BY, p_UPDATE_BY);
        SET v_chitdetail_id = LAST_INSERT_ID();
    ELSE
        SET v_chitdetail_id = p_CHITDETAIL_ID;
        UPDATE chitdetailinfo
        SET CHIT_ID = p_CHIT_ID, CHIT_CD = v_chit_cd, CHITDETAIL_CD = p_CHITDETAIL_CD, CHIT_YMD = p_CHIT_YMD,
            DEBIT = p_DEBIT, CREDIT = p_CREDIT, AMOUNT = IFNULL(p_AMOUNT, 0), FC_AMOUNT = IFNULL(p_FC_AMOUNT, 0),
            FC_TYPE = p_FC_TYPE, FC_RATE = IFNULL(p_FC_RATE, 0), FC_DATETIME = p_FC_DATETIME, SORT = IFNULL(p_SORT, 0), ISDEL = '0', UPDATE_BY = p_UPDATE_BY
        WHERE COMPANY_CD = p_COMPANY_CD AND CHITDETAIL_ID = v_chitdetail_id;
    END IF;

    INSERT INTO chitdetailinfo_ext (COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, MG_CD, MG_CD_2, MR_CD, MR_CD2, BANK_ID, BANK_CD, BANK_OWN_CD, CUSTOMER_CD, CUSTOMER_ID, CUSTOMER_OWN_CD, DEPARTMENT_ID, DEPARTMENT_CD, DEPARTMENT_CD_2, HASINVENTORY, INVENTORY_YMD, ISPAY, ISCOLLECT, ISDEL, CREATE_BY, UPDATE_BY)
    VALUES (p_COMPANY_CD, v_chitdetail_id, p_CHITDETAIL_CD, p_MG_CD, p_MG_CD_2, p_MR_CD, p_MR_CD2, v_bank_id, v_bank_cd, p_BANK_OWN_CD, v_customer_cd, v_customer_id, p_CUSTOMER_OWN_CD, v_department_id, v_department_cd, p_DEPARTMENT_CD_2, IFNULL(p_HASINVENTORY, '0'), p_INVENTORY_YMD, IFNULL(p_ISPAY, '0'), IFNULL(p_ISCOLLECT, '0'), '0', p_UPDATE_BY, p_UPDATE_BY)
    ON DUPLICATE KEY UPDATE CHITDETAIL_CD = VALUES(CHITDETAIL_CD), MG_CD = VALUES(MG_CD), MG_CD_2 = VALUES(MG_CD_2), MR_CD = VALUES(MR_CD), MR_CD2 = VALUES(MR_CD2),
                            BANK_ID = VALUES(BANK_ID), BANK_CD = VALUES(BANK_CD), BANK_OWN_CD = VALUES(BANK_OWN_CD), CUSTOMER_CD = VALUES(CUSTOMER_CD), CUSTOMER_ID = VALUES(CUSTOMER_ID), CUSTOMER_OWN_CD = VALUES(CUSTOMER_OWN_CD),
                            DEPARTMENT_ID = VALUES(DEPARTMENT_ID), DEPARTMENT_CD = VALUES(DEPARTMENT_CD), DEPARTMENT_CD_2 = VALUES(DEPARTMENT_CD_2), HASINVENTORY = VALUES(HASINVENTORY),
                            INVENTORY_YMD = VALUES(INVENTORY_YMD), ISPAY = VALUES(ISPAY), ISCOLLECT = VALUES(ISCOLLECT), ISDEL = '0', UPDATE_BY = VALUES(UPDATE_BY);

    -- Always remove previous VAT row; only re-insert when VAT_DIRECTION is provided.
    DELETE FROM chitdetailinfo_vat WHERE COMPANY_CD = p_COMPANY_CD AND CHITDETAIL_ID = v_chitdetail_id;
    IF NULLIF(UPPER(TRIM(p_VAT_DIRECTION)), '') IS NOT NULL THEN
        INSERT INTO chitdetailinfo_vat (
            COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD,
            VAT_SERIAL_NO, VAT_CHIT_NO, VAT_CHIT_NO_2,
            VAT_AMOUNT, VAT_TAXABLE_AMOUNT, FC_VAT_AMOUNT, VAT_ISFREE,
            VAT_INVOICE_CD, VAT_INVOICE_NM, VAT_INFO_TYPE,
            VAT_COMPANY_ISSUE, VAT_COMPANY_ISSUE_ADDRESS, VAT_COMPANY_ISSUE_CD, VAT_COMPANY_TAXCD,
            VAT_PRODUCT_NM, VAT_ETC, VAT_YMD, VAT_YMD_2, VAT_INQUIRY_IN, VAT_INQUIRY_CODE,
            IS_NEXTVAT, ISDEL, CREATE_BY, UPDATE_BY,
            VAT_RATE_CD, VAT_RATE_PERCENT, VAT_CUSTOMS_EXCHANGE_RATE, VAT_DIRECTION
        )
        VALUES (
            p_COMPANY_CD, v_chitdetail_id, p_CHITDETAIL_CD,
            p_VAT_SERIAL_NO, p_VAT_CHIT_NO, p_VAT_CHIT_NO_2,
            IFNULL(p_VAT_AMOUNT, 0), IFNULL(p_VAT_TAXABLE_AMOUNT, 0), IFNULL(p_FO_VAT_AMOUNT, 0), IFNULL(p_VAT_ISFREE, '0'),
            p_VAT_INVOICE_CD, p_VAT_INVOICE_NM, p_VAT_INFO_TYPE,
            p_VAT_COMPANY_ISSUE, p_VAT_COMPANY_ISSUE_ADDRESS, p_VAT_COMPANY_ISSUE_CD, p_VAT_COMPANY_TAXCD,
            p_VAT_PRODUCT_NM, p_VAT_ETC, p_VAT_YMD, p_VAT_YMD_2, p_VAT_INQUIRY_IN, p_VAT_INQUIRY_CODE,
            IFNULL(p_IS_NEXTVAT, '0'), '0', p_UPDATE_BY, p_UPDATE_BY,
            NULLIF(TRIM(p_VAT_RATE_CD), ''), p_VAT_RATE_PERCENT, p_VAT_CUSTOMS_EXCHANGE_RATE,
            NULLIF(UPPER(TRIM(p_VAT_DIRECTION)), '')
        );
    END IF;

    DELETE FROM chitdetaildescriptioninfo WHERE COMPANY_CD = p_COMPANY_CD AND CHITDETAIL_ID = v_chitdetail_id;
    IF IFNULL(TRIM(p_DETAIL_DESCRIPTION_VIET), '') <> '' THEN
        INSERT INTO chitdetaildescriptioninfo (COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, DESC_CD, LANG_TYPE, DESCRIPTION, ISDEL, CREATE_BY, UPDATE_BY)
        VALUES (p_COMPANY_CD, v_chitdetail_id, p_CHITDETAIL_CD, p_CHITDETAIL_CD, 'VIET', p_DETAIL_DESCRIPTION_VIET, '0', p_UPDATE_BY, p_UPDATE_BY);
    END IF;
    IF IFNULL(TRIM(p_DETAIL_DESCRIPTION_ENG), '') <> '' THEN
        INSERT INTO chitdetaildescriptioninfo (COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, DESC_CD, LANG_TYPE, DESCRIPTION, ISDEL, CREATE_BY, UPDATE_BY)
        VALUES (p_COMPANY_CD, v_chitdetail_id, p_CHITDETAIL_CD, p_CHITDETAIL_CD, 'ENG', p_DETAIL_DESCRIPTION_ENG, '0', p_UPDATE_BY, p_UPDATE_BY);
    END IF;
    IF IFNULL(TRIM(p_DETAIL_DESCRIPTION_KOR), '') <> '' THEN
        INSERT INTO chitdetaildescriptioninfo (COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, DESC_CD, LANG_TYPE, DESCRIPTION, ISDEL, CREATE_BY, UPDATE_BY)
        VALUES (p_COMPANY_CD, v_chitdetail_id, p_CHITDETAIL_CD, p_CHITDETAIL_CD, 'KOR', p_DETAIL_DESCRIPTION_KOR, '0', p_UPDATE_BY, p_UPDATE_BY);
    END IF;

    SELECT v_chitdetail_id AS CHITDETAIL_ID;
END//
DELIMITER ;
