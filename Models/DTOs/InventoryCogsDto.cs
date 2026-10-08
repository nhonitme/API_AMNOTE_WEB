namespace API_AMNOTE_WEB.Models.DTOs
{
    public sealed class InventoryCogsDto
    {
        public long CHIT_ID { get; set; }
        public string CHIT_CD { get; set; } = string.Empty;
        public string CHIT_NO { get; set; } = string.Empty;
        public string CHIT_YMD { get; set; } = string.Empty;
        public decimal AMOUNT { get; set; }
        public string IS_LOCK { get; set; } = string.Empty;
        public List<InventoryCogsDetailDto> DETAILS { get; set; } = new();
    }

    public sealed class InventoryCogsDetailDto
    {
        public long OUTPUT_ID { get; set; }
        public long CHITDETAIL_ID { get; set; }
        public string CHITDETAIL_CD { get; set; } = string.Empty;
        public string DEBIT { get; set; } = string.Empty;
        public string CREDIT { get; set; } = string.Empty;
        public decimal AMOUNT { get; set; }
    }
}
