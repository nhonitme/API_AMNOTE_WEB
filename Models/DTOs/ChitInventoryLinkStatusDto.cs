namespace API_AMNOTE_WEB.Models.DTOs
{
    public class ChitInventoryLinkVoucherDto
    {
        public long INVENTORY_CHIT_ID { get; set; }
        public string INVENTORY_CHIT_CD { get; set; } = string.Empty;
        public string? INVENTORY_CHIT_NO { get; set; }
        public string? INVENTORY_CHIT_YMD { get; set; }
        public string INVENTORY_CHIT_TYPE { get; set; } = string.Empty;
        public decimal LINKED_QUANTITY { get; set; }
        public int LINKED_LINE_COUNT { get; set; }
    }

    public class ChitInventoryLinkStatusDto
    {
        public long CHIT_ID { get; set; }
        public string CHIT_CD { get; set; } = string.Empty;
        public string CHIT_TYPE { get; set; } = string.Empty;
        public decimal SOURCE_TOTAL_QUANTITY { get; set; }
        public decimal LINKED_TOTAL_QUANTITY { get; set; }
        public decimal REMAINING_QUANTITY { get; set; }
        public int INVENTORY_VOUCHER_COUNT { get; set; }
        public string INVENTORY_STATUS { get; set; } = "NONE";
        public List<ChitInventoryLinkVoucherDto> INVENTORY_VOUCHERS { get; set; } = new();
        public List<long> LINKED_CHITDETAIL_IDS { get; set; } = new();
    }

    public class ChitInventorySourceVoucherDto
    {
        public long CHIT_ID { get; set; }
        public string CHIT_CD { get; set; } = string.Empty;
        public string CHIT_TYPE { get; set; } = string.Empty;
        public long CHITDETAIL_ID { get; set; }
        public string CHITDETAIL_CD { get; set; } = string.Empty;
    }
}
