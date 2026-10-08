-- TAX_VAT_REDUCTION_APPENDIX
-- Deploy to each COMPANY database (not AM_WEB_MANAGER).
-- Returns TYPE, ITEM_KIND, mhdon, json, consumed by TaxReductionAppendixCalculator.Process.
-- $WHERE is legacy trusted SQL text; do NOT pass untrusted HTTP input into it.
DROP PROCEDURE IF EXISTS `rpt_Tax_reduction_appendix`;
DELIMITER //
CREATE PROCEDURE `rpt_Tax_reduction_appendix`(
    IN `$COMPANY_CD` VARCHAR(20),
    IN `$FROM_YMD` CHAR(8),
    IN `$TO_YMD` CHAR(8),
    IN `$mhdon` VARCHAR(20),
    IN `$WHERE` VARCHAR(500)
)
BEGIN
    DECLARE v_company VARCHAR(20) DEFAULT '';
    DECLARE v_from CHAR(8) DEFAULT '';
    DECLARE v_to CHAR(8) DEFAULT '';
    DECLARE v_mhdon VARCHAR(20) DEFAULT '';
    DECLARE v_filter TEXT DEFAULT '';

    SET v_company = COALESCE(`$COMPANY_CD`, '');
    SET v_from = COALESCE(`$FROM_YMD`, '');
    SET v_to = COALESCE(`$TO_YMD`, '');
    SET v_mhdon = COALESCE(`$mhdon`, '');
    SET v_filter = COALESCE(`$WHERE`, '');

    IF v_company = '' OR v_from = '' OR v_to = '' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Company code, from date and to date are required';
    END IF;

    SET @VAT_REDUCTION_SQL = CONCAT(
        'SELECT 1 AS TYPE, ''Buying'' AS ITEM_KIND, J.mhdon, COALESCE(J.json, '''') AS json ',
        'FROM buy_list_json J ',
        'WHERE J.COMPANY_CD = ', QUOTE(v_company),
        ' AND COALESCE(J.ISDEL, ''0'') <> ''1'' ',
        'AND J.mhdon IN (SELECT F.mhdon FROM buy_list_einvoice_v2 F ',
        'WHERE F.COMPANY_CD = ', QUOTE(v_company),
        ' AND COALESCE(F.ISDEL, ''0'') <> ''1'' ',
        'AND F.mhdon LIKE ', QUOTE(CONCAT('%', v_mhdon)),
        ' AND F.tdlap BETWEEN ', QUOTE(v_from), ' AND ', QUOTE(v_to), ' ',
        v_filter, ') ',
        'UNION ALL ',
        'SELECT 2 AS TYPE, ''Selling'' AS ITEM_KIND, J.mhdon, COALESCE(J.json, '''') AS json ',
        'FROM sell_list_json J ',
        'WHERE J.COMPANY_CD = ', QUOTE(v_company),
        ' AND COALESCE(J.ISDEL, ''0'') <> ''1'' ',
        'AND J.mhdon IN (SELECT F.mhdon FROM sell_list_einvoice_v2 F ',
        'WHERE F.COMPANY_CD = ', QUOTE(v_company),
        ' AND COALESCE(F.ISDEL, ''0'') <> ''1'' ',
        'AND F.mhdon LIKE ', QUOTE(CONCAT('%', v_mhdon)),
        ' AND F.tdlap BETWEEN ', QUOTE(v_from), ' AND ', QUOTE(v_to), ' ',
        v_filter, ')'
    );
    PREPARE vat_reduction_stmt FROM @VAT_REDUCTION_SQL;
    EXECUTE vat_reduction_stmt;
    DEALLOCATE PREPARE vat_reduction_stmt;
END//
DELIMITER ;

-- Suggested DATA_SOURCE_REF in sys_report_catalog:
-- CALL rpt_Tax_reduction_appendix(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, '', '')
