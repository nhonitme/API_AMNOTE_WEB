using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceSellerSearchRequest
    {
        public string? Khhdon { get; set; }
        public long? SellerId { get; set; }
        public string? Keyword { get; set; }
        public bool IncludeInactive { get; set; }
        public bool IncludeAllTemplates { get; set; }
    }

    /// <summary>
    /// Cập nhật thông tin người bán từ web setting.
    /// SELLER_TAX_CD không nhận từ client — API giữ MST hiện có trên DB.
    /// </summary>
    public class EInvoiceSellerInfoSaveRequest
    {
        public string? SELLER_NM { get; set; }
        public string? SELLER_ADDRESS { get; set; }
        public string? MDDKDOANH { get; set; }
        public string? TDDKDOANH { get; set; }
        public string? DCDDKDOANH { get; set; }
        public string? MCHANG { get; set; }
        public string? TCHANG { get; set; }
        public string? SDTHOAI { get; set; }
        public string? DCTDTU { get; set; }
        public string? STKNHANG { get; set; }
        public string? TNHANG { get; set; }
        public string? FAX { get; set; }
        public string? WEBSITE { get; set; }
    }

    public class EInvoiceSellerPreviewDto
    {
        public long SELLER_ID { get; set; }
        public long XSL_ID { get; set; }
        public string SELLER_NM { get; set; } = string.Empty;
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? XSL_TEMPLATE_NM { get; set; }
        public string XML { get; set; } = string.Empty;
        public string XSL { get; set; } = string.Empty;
        public string HTML { get; set; } = string.Empty;
    }

    public class EInvoiceSellerDecimalPreviewRequest
    {
        public long? XslId { get; set; }
        /// <summary>VND, USD, FC, … — FC được map sang USD demo.</summary>
        public string? CurrencyCode { get; set; }
        public List<EInvoiceDecimalSettingDto>? DecimalSettings { get; set; }
    }

    public class EInvoiceDecimalSettingDto
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
    }

    public class EInvoiceDecimalSettingSaveRequest : EInvoiceDecimalSettingDto
    {
    }

    public class EInvoiceDecimalSettingSearchRequest
    {
        public long? SettingId { get; set; }
        public long? XslId { get; set; }
        public string? ApplyTarget { get; set; }
        public string? FieldScope { get; set; }
        public string? Keyword { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public class EInvoiceUserSettingDto
    {
        public long SETTING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string SETTING_KEY { get; set; } = string.Empty;
        public string? SETTING_VALUE { get; set; }
        public string VALUE_TYPE { get; set; } = "STRING";
        public int ISDEL { get; set; }
    }

    public class EInvoiceUserSettingSaveRequest : EInvoiceUserSettingDto
    {
    }

    public class EInvoiceUserSettingSearchRequest
    {
        public long? SettingId { get; set; }
        public string? UserId { get; set; }
        public string? Keyword { get; set; }
        public bool IncludeDeleted { get; set; }
    }

    public class EInvoiceAdminSettingDto
    {
        public long SETTING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string SETTING_TYPE { get; set; } = "GENERAL";
        public string SETTING_KEY { get; set; } = string.Empty;
        public string? SETTING_VALUE { get; set; }
        public string VALUE_TYPE { get; set; } = "STRING";
        public int ISDEL { get; set; }
    }

    public class EInvoiceAdminSettingSearchRequest
    {
        public long? SettingId { get; set; }
        public string? SettingType { get; set; }
        public string? Keyword { get; set; }
        public bool IncludeDeleted { get; set; }
    }
}
