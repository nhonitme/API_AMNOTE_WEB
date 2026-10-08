namespace API_AMNOTE_WEB.Models.DTOs
{
    /// <summary>
    /// Editable B03 cash-flow template for one report code and version.
    /// </summary>
    public sealed class CashFlowFormulaOptionsDto
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_CODE { get; set; } = string.Empty;
        public string REPORT_VERSION { get; set; } = string.Empty;
        public string SOURCE_COMPANY_CD { get; set; } = string.Empty;
        public string CASH_ACCOUNT_PREFIXES { get; set; } = "111,112,113";
        public List<CashFlowFormulaOptionRowDto> ROWS { get; set; } = new();
    }

    /// <summary>
    /// A DETAIL row used by the B03 direct/indirect cash-flow procedures.
    /// </summary>
    public sealed class CashFlowFormulaOptionRowDto
    {
        public long ID { get; set; }
        public string ITEM_KEY { get; set; } = string.Empty;
        public string? ITEM_CODE { get; set; }
        /// <summary>Vietnamese display text for the line (stored in template CAPTION).</summary>
        public string? CAPTION { get; set; }
        /// <summary>i18n key: prefer REPORT_CODE.ITEM_CODE else ITEM_KEY → t_message_info.KEY.</summary>
        public string? LABEL_TEXT { get; set; }
        /// <summary>
        /// Layout column within the template unique key. Formula editor collapses to one row per ITEM_KEY.
        /// </summary>
        public string? COLUMN_KEY { get; set; }
        public string ELEMENT_TYPE { get; set; } = string.Empty;
        public int LEVEL_NO { get; set; }
        public string? DATA_SOURCE_TYPE { get; set; }
        public string? FORMULA_EXPR { get; set; }
        /// <summary>GTGT dual-column indicator code for HHDV value (e.g. 27).</summary>
        public string? CODE_NO1 { get; set; }
        /// <summary>GTGT dual-column indicator code for VAT amount (e.g. 28).</summary>
        public string? CODE_NO2 { get; set; }
        /// <summary>GTGT formula for CODE_NO1 / VALUE_HHDV.</summary>
        public string? FORMULA_NO1 { get; set; }
        /// <summary>GTGT formula for CODE_NO2 / VAT_AMOUNT.</summary>
        public string? FORMULA_NO2 { get; set; }
        /// <summary>GTGT calc method for CODE_NO1: MANUAL / FORMULA / FORMULA_POSITIVE / FORMULA_NEGATIVE.</summary>
        public string? CALC_METHOD_NO1 { get; set; }
        /// <summary>GTGT calc method for CODE_NO2.</summary>
        public string? CALC_METHOD_NO2 { get; set; }
        public int? DIRECT_RULE_FLOW_SIGN { get; set; }
        public string? DIRECT_RULE_ACC_PREFIX { get; set; }
        public int? DIRECT_RULE_PRIORITY { get; set; }
        public int? DIRECT_RULE_FLOW_SIGN_2 { get; set; }
        public string? DIRECT_RULE_ACC_PREFIX_2 { get; set; }
        public int? DIRECT_RULE_PRIORITY_2 { get; set; }
        public string? ACCOUNT_RULE { get; set; }
        public string? CALC_METHOD { get; set; }
        /// <summary>
        /// Human-readable summary of the effective formula or account rule for this row.
        /// </summary>
        public string? FORMULA_DISPLAY { get; set; }
        public int SORT_ORDER { get; set; }
        public string FONT_BOLD { get; set; } = "0";
        public string FONT_ITALIC { get; set; } = "0";
        public string IS_VISIBLE { get; set; } = "1";
    }

    public sealed class SaveCashFlowFormulaOptionsRequest
    {
        public string? REPORT_CODE { get; set; }
        public string? REPORT_VERSION { get; set; }
        public string? CASH_ACCOUNT_PREFIXES { get; set; }
        public List<SaveCashFlowFormulaOptionRowRequest>? ROWS { get; set; }
    }

    public sealed class PreviewCashFlowFormulaOptionsRequest
    {
        public string? REPORT_CODE { get; set; }
        public string? REPORT_VERSION { get; set; }
        public string? CASH_ACCOUNT_PREFIXES { get; set; }
        public List<SaveCashFlowFormulaOptionRowRequest>? ROWS { get; set; }
        public string? PreviewReportCode { get; set; }
        public string? FromYmd { get; set; }
        public string? ToYmd { get; set; }
        public string? UnitDivisor { get; set; }
        public string? MenuCode { get; set; }
    }

    public sealed class FormulaOptionPreviewDto
    {
        public List<FormulaOptionPreviewColumnDto> COLUMNS { get; set; } = new();
        public List<FormulaOptionPreviewRowDto> ROWS { get; set; } = new();
    }

    public sealed class FormulaOptionPreviewColumnDto
    {
        public string FIELD_NAME { get; set; } = string.Empty;
        public string CAPTION { get; set; } = string.Empty;
    }

    public sealed class FormulaOptionPreviewRowDto
    {
        public string? ITEM_CODE { get; set; }
        public Dictionary<string, object?> VALUES { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SaveCashFlowFormulaOptionRowRequest
    {
        // ID is informational. ITEM_KEY is the stable row identity when saving a full draft.
        public long? ID { get; set; }
        public string? ITEM_KEY { get; set; }
        public string? ITEM_CODE { get; set; }
        public string? CAPTION { get; set; }
        public string? LABEL_TEXT { get; set; }
        /// <summary>Legacy alias for CAPTION on save requests.</summary>
        public string? ITEM_NAME { get; set; }
        public string? ELEMENT_TYPE { get; set; }
        public int? LEVEL_NO { get; set; }
        public string? DATA_SOURCE_TYPE { get; set; }
        public string? FORMULA_EXPR { get; set; }
        public string? CODE_NO1 { get; set; }
        public string? CODE_NO2 { get; set; }
        public string? FORMULA_NO1 { get; set; }
        public string? FORMULA_NO2 { get; set; }
        public string? CALC_METHOD_NO1 { get; set; }
        public string? CALC_METHOD_NO2 { get; set; }
        public int? DIRECT_RULE_FLOW_SIGN { get; set; }
        public string? DIRECT_RULE_ACC_PREFIX { get; set; }
        public int? DIRECT_RULE_PRIORITY { get; set; }
        public int? DIRECT_RULE_FLOW_SIGN_2 { get; set; }
        public string? DIRECT_RULE_ACC_PREFIX_2 { get; set; }
        public int? DIRECT_RULE_PRIORITY_2 { get; set; }
        public string? ACCOUNT_RULE { get; set; }
        public string? CALC_METHOD { get; set; }
        public int? SORT_ORDER { get; set; }
        public string? FONT_BOLD { get; set; }
        public string? FONT_ITALIC { get; set; }
        public string? IS_VISIBLE { get; set; }
    }
}
