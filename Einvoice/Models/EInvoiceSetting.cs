namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceTemplateDesignerDraft
    {
        public long DESIGN_ID { get; set; }
        public long CONTENT_ID { get; set; }
        public long XSL_ID { get; set; }
        public int VERSION_NO { get; set; }
        public string SOURCE_TYPE { get; set; } = "USER";
        public string? DESIGN_NM { get; set; }
        public string? XSL_CONTENT { get; set; }
        public string? LOGO_PATH { get; set; }
        public string? INVOICE_BACKGROUND_PATH { get; set; }
        public string? INVOICE_BORDER_PATH { get; set; }
        public string? BACKGROUND_PATH { get; set; }
    }

    public class EInvoiceTemplateDesignerDraftSaveRequest
    {
        public long DESIGN_ID { get; set; }
        public string? XSL_CONTENT { get; set; }
        public string? LOGO_PATH { get; set; }
        public string? BACKGROUND_PATH { get; set; }
        public string? INVOICE_BACKGROUND_PATH { get; set; }
        public string? INVOICE_BORDER_PATH { get; set; }
    }

    public class EInvoiceFtpImageFile
    {
        public string FILE_NAME { get; set; } = string.Empty;
        public string PATH { get; set; } = string.Empty;
        public long SIZE { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        /// <summary>library = shared FTP catalog; company = uploaded by the current company.</summary>
        public string SOURCE { get; set; } = "library";
    }

    public class EInvoiceFtpXslFile
    {
        public string FILE_NAME { get; set; } = string.Empty;
        public string PATH { get; set; } = string.Empty;
    }

    public class EInvoiceFtpImageSelectRequest
    {
        public string? FILE_NAME { get; set; }
        public string? PATH { get; set; }
    }

    public class EInvoiceFtpXslSelectRequest
    {
        public string? FILE_NAME { get; set; }
    }

    public class EInvoiceDesignerImageResult
    {
        public string PATH { get; set; } = string.Empty;
        public string FILE_NAME { get; set; } = string.Empty;
        public string IMAGE_KIND { get; set; } = string.Empty;
        public long DESIGN_ID { get; set; }
    }

    public class EInvoiceFtpXslContentResult
    {
        public string FILE_NAME { get; set; } = string.Empty;
        public string PATH { get; set; } = string.Empty;
        public string XSL_CONTENT { get; set; } = string.Empty;
    }

    public class EInvoiceSellerXslTemplateMetaSaveRequest
    {
        public long XSL_ID { get; set; }
        public string? TEMPLATE_NM { get; set; }
        public string? THDON { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? FROM_SHDON { get; set; }
        public string? TO_SHDON { get; set; }
        public int USE_MULTI_TAX_RATE { get; set; }
        /// <summary>FTP sample file name under EinvoiceXSL. Required when creating.</summary>
        public string? XSL_FILE_NAME { get; set; }
        /// <summary>Optional raw XSL body. Used when XSL_FILE_NAME is empty.</summary>
        public string? XSL_CONTENT { get; set; }
    }

    public class EInvoiceSellerXslTemplateMetaResult
    {
        public long XSL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string TEMPLATE_CD { get; set; } = string.Empty;
        public string? TEMPLATE_NM { get; set; }
        public string? THDON { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? FROM_SHDON { get; set; }
        public string? TO_SHDON { get; set; }
        public int USE_MULTI_TAX_RATE { get; set; }
        public int VERSION_NO { get; set; }
        public int IS_DEFAULT { get; set; }
        public int IS_ACTIVE { get; set; }
    }

    public class EInvoiceSellerXslTemplateContentResult
    {
        public long XSL_ID { get; set; }
        public long CURRENT_DESIGN_ID { get; set; }
        public string? XSL_CONTENT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceSellerXslTemplateDeleteManyRequest
    {
        public List<long> XslIds { get; set; } = new();
    }

    public class EInvoiceTemplateDesignerDesign
    {
        public long DESIGN_ID { get; set; }
        public long XSL_ID { get; set; }
        public long CONTENT_ID { get; set; }
        public string DESIGN_NM { get; set; } = string.Empty;
        public bool IS_EDITABLE { get; set; }
        public int TEMPLATE_IS_ACTIVE { get; set; }
        public string? XSL_CONTENT { get; set; }
        public string? LOGO_PATH { get; set; }
        public string? INVOICE_BACKGROUND_PATH { get; set; }
        public string? INVOICE_BORDER_PATH { get; set; }
        public string? BACKGROUND_PATH { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceDecimalSetting
    {
        public long SETTING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long XSL_ID { get; set; }
        public string APPLY_TARGET { get; set; } = string.Empty;
        public string FIELD_SCOPE { get; set; } = string.Empty;
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string CURRENCY_SCOPE { get; set; } = "ANY";
        public int DECIMAL_SCALE { get; set; } = 2;
        public string ROUND_MODE { get; set; } = "ROUND";
        public int IS_ACTIVE { get; set; } = 1;
        public int SORT_ORDER { get; set; }
        public string NOTE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceUserSetting
    {
        public long SETTING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string SETTING_KEY { get; set; } = string.Empty;
        public string? SETTING_VALUE { get; set; }
        public string VALUE_TYPE { get; set; } = "STRING";
        public int ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceAdminSetting
    {
        public long SETTING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string SETTING_TYPE { get; set; } = "GENERAL";
        public string SETTING_KEY { get; set; } = string.Empty;
        public string? SETTING_VALUE { get; set; }
        public string VALUE_TYPE { get; set; } = "STRING";
        public int ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}
