using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Built-in Excel template columns used when manager DB
    /// <c>excel_template_column</c> has no rows for a module.
    /// </summary>
    public static class ExcelTemplateDefaults
    {
        public static IReadOnlyList<ExcelTemplateColumnInfo> GetColumns(string moduleCd)
        {
            if (string.IsNullOrWhiteSpace(moduleCd))
            {
                return Array.Empty<ExcelTemplateColumnInfo>();
            }

            return moduleCd.Trim() switch
            {
                "OpeningBalanceAccount" => SharedAmountColumns(),
                "OpeningBalanceBank" => WithEntityCode("BANK_CD", SharedAmountColumns()),
                "OpeningBalanceCustomer" => WithEntityCode("CUSTOMER_CD", SharedAmountColumns()),
                "OpeningBalanceCostObject" => WithEntityCode("DEPARTMENT_CD", SharedAmountColumns()),
                "InventoryOpening" =>
                [
                    Col("PRODUCT_CD", required: true),
                    Col("STORE_CD", required: true),
                    Col("UNIT_CD"),
                    Col("QUANTITY", required: true),
                    Col("UNIT_PRICE_CC", required: true),
                    Col("AMOUNT_CC"),
                    Col("SUMMARY"),
                ],
                "FixedAssetInfo" =>
                [
                    Col("ASSET_CD", required: true),
                    Col("ASSET_NM", required: true),
                    Col("ACC_CD"),
                    Col("USE_DEPT_CD"),
                    Col("RECEIVE_YMD"),
                    Col("USE_START_YMD", required: true),
                    Col("USEFUL_LIFE_MONTH", required: true),
                    Col("ORIGINAL_AMT", required: true),
                    Col("ACCUM_DEPRE_AMT"),
                    Col("STATUS"),
                    Col("ACQ_CHIT_NO"),
                    Col("NOTE"),
                    Col("ALLOC_TYPE"),
                    Col("ALLOC_RATE"),
                    Col("DEBIT_ACCT_CD", required: true),
                    Col("CREDIT_ACCT_CD"),
                    Col("DEPARTMENT_CD"),
                    Col("ALLOC_NOTE"),
                ],
                _ => Array.Empty<ExcelTemplateColumnInfo>()
            };
        }

        private static List<ExcelTemplateColumnInfo> SharedAmountColumns()
            =>
            [
                Col("ACC_CD", required: true),
                Col("FC_TYPE", required: true),
                Col("DEBIT"),
                Col("CREDIT"),
                Col("DEBIT_FC"),
                Col("CREDIT_FC"),
                Col("EXCHANGE_RATE"),
                Col("NOTE"),
            ];

        private static List<ExcelTemplateColumnInfo> WithEntityCode(
            string entityCodeKey,
            List<ExcelTemplateColumnInfo> amountColumns)
        {
            var columns = new List<ExcelTemplateColumnInfo>(amountColumns.Count + 1)
            {
                Col(entityCodeKey, required: true)
            };
            columns.AddRange(amountColumns);
            return columns;
        }

        private static ExcelTemplateColumnInfo Col(string key, bool required = false)
            => new()
            {
                FIELD_NAME = key,
                IS_REQUIRED = required ? "1" : "0",
                IS_PRIMARY_KEY = "0",
                TABLE_NM = string.Empty,
                EXPLAIN_TABLE_QUERY = null
            };
    }
}
