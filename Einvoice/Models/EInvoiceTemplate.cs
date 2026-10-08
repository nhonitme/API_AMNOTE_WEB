namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceTemplate
    {
        public long TEMPLATE_ID { get; set; }
        public string TEMPLATE_TYPE { get; set; } = string.Empty;
        public string TEMPLATE_CD { get; set; } = string.Empty;
        public string TEMPLATE_NM { get; set; } = string.Empty;
        public string? SUBJECT { get; set; }
        public string CONTENT { get; set; } = string.Empty;
        public int IS_ACTIVE { get; set; }
        public int ISDEL { get; set; }
    }
}
