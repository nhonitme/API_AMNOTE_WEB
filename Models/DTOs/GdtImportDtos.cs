using System.Text.Json;

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class GdtImportUpsertRequest
    {
        /// <summary>BUY = mua vào, SELL = bán ra.</summary>
        public string Type { get; set; } = "BUY";

        /// <summary>Mảng hóa đơn từ GDT (summary và/hoặc detail).</summary>
        public List<JsonElement>? Items { get; set; }
    }

    public class GdtImportUpsertResult
    {
        public string Type { get; set; } = "BUY";
        public int Received { get; set; }
        public int Upserted { get; set; }
        public int Skipped { get; set; }
        public int JsonSaved { get; set; }
        public int ListSaved { get; set; }
        public int NeedDetailCount { get; set; }
        public List<GdtImportListItemState> Items { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>Trạng thái từng HĐ sau khi lưu list — dùng cho CanSkipDetailFetch.</summary>
    public class GdtImportListItemState
    {
        public string mhdon { get; set; } = "";
        public bool HasList { get; set; }
        public bool HasJson { get; set; }
        public bool StatusChanged { get; set; }
        public bool ListSaved { get; set; }
        /// <summary>true = cần gọi detail (không skip).</summary>
        public bool NeedDetail { get; set; }
        public string? Error { get; set; }
    }

    public class GdtImportInvoiceRow
    {
        public string mhdon { get; set; } = "";
        public string tthai { get; set; } = "";
        public string khmshdon { get; set; } = "";
        public string khhdon { get; set; } = "";
        public string tdlap { get; set; } = "";
        public string nky { get; set; } = "";
        public string shdon { get; set; } = "";
        public string dvtte { get; set; } = "";
        public string mtdtchieu { get; set; } = "";
        public string nbten { get; set; } = "";
        public string nbdchi { get; set; } = "";
        public string nbmst { get; set; } = "";
        public string nmten { get; set; } = "";
        public string nmdchi { get; set; } = "";
        public string nmmst { get; set; } = "";
        public double tgia { get; set; } = 1;
        public double tgtcthue { get; set; }
        public double tgtthue { get; set; }
        public double tgtttbso { get; set; }
        public double tgtphi { get; set; }
        public double ttcktmai { get; set; }
        public string GChu { get; set; } = "";
        public string AUTO_CHIT_CD { get; set; } = "";
        public string IS_ATTACH_FILE { get; set; } = "0";
        public string ISDEL { get; set; } = "";
        public string USERID { get; set; } = "";
        public string? JsonPayload { get; set; }
        public bool HasDetailJson { get; set; }
    }

    public class GdtExistingInvoice
    {
        public bool HasList { get; set; }
        public bool HasJson { get; set; }
        public string Status { get; set; } = "";
    }

    /// <summary>Tài khoản GDT đã lưu (info_login_tax_office_einvoice).</summary>
    public class GdtSavedLoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>Bearer token GDT đã lưu (cột TOKEN).</summary>
        public string? Token { get; set; }
        public string IsDefault { get; set; } = "0";
    }

    public class GdtSaveLoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        /// <summary>Token mới sau login GDT — null/empty giữ TOKEN cũ trên DB.</summary>
        public string? Token { get; set; }
    }
}
