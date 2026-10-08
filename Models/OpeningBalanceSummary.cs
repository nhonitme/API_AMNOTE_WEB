namespace API_AMNOTE_WEB.Models
{
    public class OpeningBalanceSummary
    {
        public long ID { get; set; }
        public string ITEM_CD { get; set; } = string.Empty;
        public string ITEM_NAME_VIET { get; set; } = string.Empty;
        public string ITEM_NAME_ENG { get; set; } = string.Empty;
        public string ITEM_NAME_KOR { get; set; } = string.Empty;
        public string ITEM_NAME_CHINA { get; set; } = string.Empty;        
        public int RECORD_COUNT { get; set; }
        public decimal TOTAL_DEBIT { get; set; }
        public decimal TOTAL_CREDIT { get; set; }
        public decimal DIFF_AMOUNT { get; set; }
        public decimal TOTAL_DEBIT_FC { get; set; }
        public decimal TOTAL_CREDIT_FC { get; set; }
        public decimal DIFF_AMOUNT_FC { get; set; }
        public string STATUS { get; set; } = string.Empty;
        public string ROUTE { get; set; } = string.Empty;
        public string NOTE { get; set; } = string.Empty;
    }
}
