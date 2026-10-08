-- Run on AM_WEB_MANAGER after deploying rpt_Tax_reduction_appendix.sql
-- to the relevant company databases. This is a configuration-only update.
-- Frontend report query parameters: status, invoiceKind, fcType.
-- Backend resolves the @p_* placeholders from those query parameters.

UPDATE sys_report_catalog
SET DATA_SOURCE_REF =
        'CALL rpt_Tax_reduction_appendix(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, @p_STATUS, @p_INVOICE_KIND, @p_FC_TYPE)',
    UPDATED_AT = NOW()
WHERE REPORT_CODE = 'TAX_VAT_REDUCTION_APPENDIX'
  AND IFNULL(ISDEL, '0') <> '1';

SELECT REPORT_CODE, DATA_SOURCE_TYPE, DATA_SOURCE_REF, IS_ACTIVE, ISDEL
FROM sys_report_catalog
WHERE REPORT_CODE = 'TAX_VAT_REDUCTION_APPENDIX';

-- If the API caches report catalog / company mapping data, refresh that cache
-- (or restart the API) after applying this change.
