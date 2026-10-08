-- TAX_VAT_REDUCTION_APPENDIX
-- Deploy to each COMPANY database (not AM_WEB_MANAGER).
-- Returns TYPE, ITEM_KIND, mhdon, json for TaxReductionAppendixCalculator.Process.
-- Parameters map from report-viewer query:
--   status      -> $TTHAI     (F.tthai)
--   invoiceKind -> $KHMSHDON  (F.khmshdon)
--   fcType      -> $DVTTE     (F.dvtte, comma-separated currency codes)
-- Blank/NULL optional filters mean All. SQL is compatible with MySQL 5.6/8.x.
-- Keep source tables buy_list_einvoice / sell_list_einvoice.

DROP PROCEDURE IF EXISTS `rpt_Tax_reduction_appendix`;
DELIMITER //

CREATE PROCEDURE `rpt_Tax_reduction_appendix`(
    IN `$COMPANY_CD` VARCHAR(20),
    IN `$FROM_YMD` CHAR(8),
    IN `$TO_YMD` CHAR(8),
    IN `$TTHAI` VARCHAR(20),
    IN `$KHMSHDON` VARCHAR(20),
    IN `$DVTTE` VARCHAR(500)
)
BEGIN
    DECLARE v_tthai VARCHAR(20) DEFAULT '';
    DECLARE v_khmshdon VARCHAR(20) DEFAULT '';
    DECLARE v_dvtte VARCHAR(500) DEFAULT '';

    IF `$COMPANY_CD` IS NULL OR TRIM(`$COMPANY_CD`) = ''
        OR `$FROM_YMD` IS NULL OR TRIM(`$FROM_YMD`) = ''
        OR `$TO_YMD` IS NULL OR TRIM(`$TO_YMD`) = ''
    THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Company code, from date and to date are required';
    END IF;

    SET v_tthai = TRIM(COALESCE(`$TTHAI`, ''));
    SET v_khmshdon = TRIM(COALESCE(`$KHMSHDON`, ''));
    SET v_dvtte = UPPER(REPLACE(TRIM(COALESCE(`$DVTTE`, '')), ' ', ''));

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
            AND (v_tthai = '' OR TRIM(COALESCE(F.tthai, '')) = v_tthai)
            AND (v_khmshdon = '' OR TRIM(COALESCE(F.khmshdon, '')) = v_khmshdon)
            AND (v_dvtte = '' OR FIND_IN_SET(UPPER(TRIM(COALESCE(F.dvtte, ''))), v_dvtte) > 0)
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
            AND (v_tthai = '' OR TRIM(COALESCE(F.tthai, '')) = v_tthai)
            AND (v_khmshdon = '' OR TRIM(COALESCE(F.khmshdon, '')) = v_khmshdon)
            AND (v_dvtte = '' OR FIND_IN_SET(UPPER(TRIM(COALESCE(F.dvtte, ''))), v_dvtte) > 0)
      );
END//

DELIMITER ;

-- Deploy manager configuration separately (AM_WEB_MANAGER):
-- UPDATE sys_report_catalog
-- SET DATA_SOURCE_REF =
--       'CALL rpt_Tax_reduction_appendix(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, @p_STATUS, @p_INVOICE_KIND, @p_FC_TYPE)',
--     UPDATED_AT = NOW()
-- WHERE REPORT_CODE = 'TAX_VAT_REDUCTION_APPENDIX' AND IFNULL(ISDEL, '0') <> '1';

-- Company DB tests:
-- CALL rpt_Tax_reduction_appendix('0001', '20260801', '20260831', '', '', '');
-- CALL rpt_Tax_reduction_appendix('0001', '20260801', '20260831', '1', '1', 'VND');
-- CALL rpt_Tax_reduction_appendix('0001', '20260801', '20260831', '', '', 'VND,USD');
