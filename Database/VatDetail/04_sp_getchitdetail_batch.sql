-- =============================================================================
-- VAT Detail: update getchitdetail_batch — return VAT_DIRECTION / rate / customs
-- Target: company database
-- Keeps legacy placeholders (NULL AS CHITDETAIL_VAT_CD, 0 AS PERCENT) for
-- modules that still read those aliases; VAT Panel must use VAT_RATE_* columns.
-- Safe to re-run (DROP + CREATE).
-- Do NOT run on production without review.
-- =============================================================================

DROP PROCEDURE IF EXISTS `getchitdetail_batch`;
DELIMITER //
CREATE PROCEDURE `getchitdetail_batch`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_INPUT_TYPE VARCHAR(20),
    IN p_CHIT_IDS TEXT
)
BEGIN
    DECLARE v_chit_ids TEXT;
    SET v_chit_ids = REPLACE(IFNULL(TRIM(p_CHIT_IDS), ''), '|', ',');
    DROP TEMPORARY TABLE IF EXISTS tmp_filter_ids;
    CREATE TEMPORARY TABLE tmp_filter_ids (
      ID BIGINT UNSIGNED NOT NULL PRIMARY KEY
    ) ENGINE=InnoDB;
    IF IFNULL(v_chit_ids, '') <> '' THEN
      INSERT IGNORE INTO tmp_filter_ids (ID)
      SELECT DISTINCT CAST(jt.val AS UNSIGNED)
      FROM JSON_TABLE(
        CONCAT('["', REPLACE(REPLACE(v_chit_ids, '|', ','), ',', '","'), '"]'),
        '$[*]' COLUMNS (val VARCHAR(64) PATH '$')
      ) jt
      WHERE NULLIF(TRIM(jt.val), '') IS NOT NULL
        AND jt.val REGEXP '^[0-9]+$';
    END IF;

    SELECT
        d.CHITDETAIL_ID,
        d.COMPANY_CD,
        d.CHIT_ID,
        d.CHITDETAIL_CD,
        d.CHIT_YMD,
        NULL AS CHIT_VMD,
        d.DEBIT,
        debit_acc.ACCTITLE_NM_VIET AS DEBIT_NM_VIET,
        debit_acc.ACCTITLE_NM_ENG AS DEBIT_NM_ENG,
        debit_acc.ACCTITLE_NM_KOR AS DEBIT_NM_KOR,
        debit_acc.ACCTITLE_NM_CHINA AS DEBIT_NM_CHINA,
        d.CREDIT,
        credit_acc.ACCTITLE_NM_VIET AS CREDIT_NM_VIET,
        credit_acc.ACCTITLE_NM_ENG AS CREDIT_NM_ENG,
        credit_acc.ACCTITLE_NM_KOR AS CREDIT_NM_KOR,
        credit_acc.ACCTITLE_NM_CHINA AS CREDIT_NM_CHINA,
        d.AMOUNT,
        d.FC_AMOUNT,
        d.FC_TYPE,
        d.FC_RATE,
        d.FC_DATETIME,
        d.SORT,
        d.ISDEL,
        NULL AS CHITDETAIL_VAT_CD,
        ext.MG_CD,
        ext.MG_CD_2,
        ext.MR_CD,
        ext.MR_CD2,
        ext.BANK_ID,
        COALESCE(bank.BANK_CD, ext.BANK_CD) AS BANK_CD,
        ext.BANK_OWN_CD,
        COALESCE(customer.CUSTOMER_CD, ext.CUSTOMER_CD) AS CUSTOMER_CD,
        ext.CUSTOMER_ID,
        customer.CUSTOMER_NM_VIET,
        customer.CUSTOMER_NM_ENG,
        customer.CUSTOMER_NM_KOR,
        customer.CUSTOMER_NM_CHINA,
        ext.CUSTOMER_OWN_CD,
        ext.DEPARTMENT_ID,
        COALESCE(department.DEPARTMENT_CD, ext.DEPARTMENT_CD) AS DEPARTMENT_CD,
        ext.DEPARTMENT_CD_2,
        ext.HASINVENTORY,
        ext.INVENTORY_YMD,
        ext.ISPAY,
        ext.ISCOLLECT,
        NULL AS VAT_INPUT_CD,
        NULL AS PRODUCT_NM,
        NULL AS UNIT_NM,
        0 AS QUANTITY,
        0 AS UNIT_PRICE,
        0 AS PRODUCT_AMOUNT,
        NULL AS VAT_TYPE,
        0 AS PERCENT,
        0 AS PRODUCT_VAT_AMOUNT,
        NULL AS PRODUCT_NOTE,
        vat.VAT_SERIAL_NO,
        vat.VAT_CHIT_NO,
        vat.VAT_CHIT_NO_2,
        vat.VAT_AMOUNT,
        vat.VAT_TAXABLE_AMOUNT,
        vat.FC_VAT_AMOUNT AS FO_VAT_AMOUNT,
        vat.VAT_ISFREE,
        vat.VAT_INVOICE_CD,
        vat.VAT_INVOICE_NM,
        vat.VAT_INFO_TYPE,
        vat.VAT_COMPANY_ISSUE,
        vat.VAT_COMPANY_ISSUE_ADDRESS,
        vat.VAT_COMPANY_ISSUE_CD,
        vat.VAT_COMPANY_TAXCD,
        vat.VAT_PRODUCT_NM,
        vat.VAT_ETC,
        vat.VAT_YMD,
        vat.VAT_YMD_2,
        vat.VAT_INQUIRY_IN,
        vat.VAT_INQUIRY_CODE,
        vat.IS_NEXTVAT,
        vat.VAT_DIRECTION,
        vat.VAT_RATE_CD,
        vat.VAT_RATE_PERCENT,
        vat.VAT_CUSTOMS_EXCHANGE_RATE,
        NULL AS UNDEFINE,
        detail_desc_vi.DESCRIPTION AS DETAIL_DESCRIPTION_VIET,
        detail_desc_en.DESCRIPTION AS DETAIL_DESCRIPTION_ENG,
        detail_desc_ko.DESCRIPTION AS DETAIL_DESCRIPTION_KOR,
        d.CREATE_BY,
        d.CREATE_AT,
        d.UPDATE_BY,
        d.UPDATE_AT
    FROM chitdetailinfo d
    INNER JOIN chitinfo h
        ON h.COMPANY_CD = d.COMPANY_CD
       AND h.CHIT_ID = d.CHIT_ID
    LEFT JOIN chitdetailinfo_ext ext
        ON ext.COMPANY_CD = d.COMPANY_CD
       AND ext.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND IFNULL(ext.ISDEL, '0') = '0'
    LEFT JOIN chitdetailinfo_vat vat
        ON vat.COMPANY_CD = d.COMPANY_CD
       AND vat.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND IFNULL(vat.ISDEL, '0') = '0'
    LEFT JOIN customer_info customer
        ON customer.COMPANY_CD = d.COMPANY_CD
       AND customer.CUSTOMER_ID = ext.CUSTOMER_ID
       AND IFNULL(customer.ISDEL, '0') = '0'
    LEFT JOIN bank_info bank
        ON bank.COMPANY_CD = d.COMPANY_CD
       AND bank.BANK_ID = ext.BANK_ID
       AND IFNULL(bank.ISDEL, '0') = '0'
    LEFT JOIN department_info department
        ON department.COMPANY_CD = d.COMPANY_CD
       AND department.DEPARTMENT_ID = ext.DEPARTMENT_ID
       AND IFNULL(department.ISDEL, '0') = '0'
    LEFT JOIN acclist_info debit_acc
        ON debit_acc.COMPANY_CD = d.COMPANY_CD
       AND debit_acc.ACC_CD = d.DEBIT
       AND IFNULL(debit_acc.ISDEL, '0') = '0'
    LEFT JOIN acclist_info credit_acc
        ON credit_acc.COMPANY_CD = d.COMPANY_CD
       AND credit_acc.ACC_CD = d.CREDIT
       AND IFNULL(credit_acc.ISDEL, '0') = '0'
    LEFT JOIN chitdetaildescriptioninfo detail_desc_vi
        ON detail_desc_vi.COMPANY_CD = d.COMPANY_CD
       AND detail_desc_vi.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND detail_desc_vi.LANG_TYPE = 'VIET'
       AND IFNULL(detail_desc_vi.ISDEL, '0') = '0'
    LEFT JOIN chitdetaildescriptioninfo detail_desc_en
        ON detail_desc_en.COMPANY_CD = d.COMPANY_CD
       AND detail_desc_en.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND detail_desc_en.LANG_TYPE = 'ENG'
       AND IFNULL(detail_desc_en.ISDEL, '0') = '0'
    LEFT JOIN chitdetaildescriptioninfo detail_desc_ko
        ON detail_desc_ko.COMPANY_CD = d.COMPANY_CD
       AND detail_desc_ko.CHITDETAIL_ID = d.CHITDETAIL_ID
       AND detail_desc_ko.LANG_TYPE = 'KOR'
       AND IFNULL(detail_desc_ko.ISDEL, '0') = '0'
    WHERE d.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(h.INPUT_TYPE, '') = IFNULL(p_INPUT_TYPE, '')
      AND IFNULL(h.ISDEL, '0') = '0'
      AND IFNULL(d.ISDEL, '0') = '0'
      AND v_chit_ids <> ''
      AND EXISTS (SELECT 1 FROM tmp_filter_ids f WHERE f.ID = (d.CHIT_ID))
    ORDER BY d.CHIT_ID, IFNULL(d.SORT, 0), d.CHITDETAIL_ID;
END//
DELIMITER ;
