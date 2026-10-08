-- TAX_VAT_REDUCTION_APPENDIX
-- Deploy to each COMPANY database (not AM_WEB_MANAGER).
-- Returns TYPE, ITEM_KIND, mhdon, json, consumed by TaxReductionAppendixCalculator.Process.
-- Only company and invoice date range are used as filters.
-- Source invoice tables: buy_list_einvoice / sell_list_einvoice (as in WinForms).

DROP PROCEDURE IF EXISTS `rpt_Tax_reduction_appendix`;
DELIMITER //

CREATE PROCEDURE `rpt_Tax_reduction_appendix`(
    IN `$COMPANY_CD` VARCHAR(20),
    IN `$FROM_YMD` CHAR(8),
    IN `$TO_YMD` CHAR(8)
)
BEGIN
    IF `$COMPANY_CD` IS NULL OR TRIM(`$COMPANY_CD`) = ''
        OR `$FROM_YMD` IS NULL OR TRIM(`$FROM_YMD`) = ''
        OR `$TO_YMD` IS NULL OR TRIM(`$TO_YMD`) = ''
    THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Company code, from date and to date are required';
    END IF;

    SELECT
        '1' AS TYPE,
        'Buying' AS ITEM_KIND,
        J.mhdon,
        IFNULL(J.json, '') AS json
    FROM buy_list_json AS J
    WHERE J.COMPANY_CD = `$COMPANY_CD`
      AND IFNULL(J.ISDEL, '') <> '1'
      AND J.mhdon IN (
          SELECT F.mhdon
          FROM buy_list_einvoice AS F
          WHERE F.COMPANY_CD = `$COMPANY_CD`
            AND IFNULL(F.ISDEL, '') <> '1'
            AND F.tdlap BETWEEN `$FROM_YMD` AND `$TO_YMD`
      )

    UNION ALL

    SELECT
        '2' AS TYPE,
        'Selling' AS ITEM_KIND,
        J.mhdon,
        IFNULL(J.json, '') AS json
    FROM sell_list_json AS J
    WHERE J.COMPANY_CD = `$COMPANY_CD`
      AND IFNULL(J.ISDEL, '') <> '1'
      AND J.mhdon IN (
          SELECT F.mhdon
          FROM sell_list_einvoice AS F
          WHERE F.COMPANY_CD = `$COMPANY_CD`
            AND IFNULL(F.ISDEL, '') <> '1'
            AND F.tdlap BETWEEN `$FROM_YMD` AND `$TO_YMD`
      );
END//

DELIMITER ;

-- Run separately on the manager database (AM_WEB_MANAGER):
-- UPDATE sys_report_catalog
-- SET DATA_SOURCE_REF = 'CALL rpt_Tax_reduction_appendix(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD)',
--     UPDATED_AT = NOW()
-- WHERE REPORT_CODE = 'TAX_VAT_REDUCTION_APPENDIX' AND IFNULL(ISDEL,'0') <> '1';
--
-- Test in the company database:
-- CALL rpt_Tax_reduction_appendix('0001', '20260801', '20260831');
