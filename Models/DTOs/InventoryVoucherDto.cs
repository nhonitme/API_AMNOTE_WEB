using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class InventoryVoucherDto
    {
        public InventoryCogsDto? COGS { get; set; }
        public long CHIT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string CHIT_CD { get; set; } = string.Empty;
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string CHIT_TYPE { get; set; } = string.Empty;
        public string IS_COGS { get; set; } = "0";
        public string INPUT_TYPE { get; set; } = string.Empty;
        public decimal? AMOUNT { get; set; }
        public decimal? TOTAL_QTY { get; set; }
        public string? REMARK { get; set; }
        public long? CUSTOMER_ID { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public string? PAYER_INFO { get; set; }
        public string? ISDEL { get; set; }
        public string? IS_LOCK { get; set; }
        public string? ISEXCEL { get; set; }
        public string? EMAIL_EPAY { get; set; }
        public string? IS_CONFIRMED { get; set; }
        public string? NOTE { get; set; }
        public int? DAY_OF_PAYMENT { get; set; }
        public string? TIME_FOR_PAYMENT { get; set; }
        public string? IS_PAYMENT { get; set; }
        public string? CHIT_CD_COGS { get; set; }
        public string? DESCRIPTION_VIET { get; set; }
        public string? DESCRIPTION_ENG { get; set; }
        public string? DESCRIPTION_KOR { get; set; }
        public List<InventoryInput> INPUTS { get; set; } = new();
        public List<InventoryOutput> OUTPUTS { get; set; } = new();
    }

    public class InventoryVoucherRequest
    {
        public long? CHIT_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? INPUT_TYPE { get; set; }
        public string? CHIT_CD { get; set; }
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_TYPE { get; set; }
        public string? IS_COGS { get; set; }
        public decimal? COGS_AMOUNT { get; set; }
        public bool? CLEAR_COGS_LINK { get; set; }
        public decimal? AMOUNT { get; set; }
        public decimal? TOTAL_QTY { get; set; }
        public string? REMARK { get; set; }
        public long? CUSTOMER_ID { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public bool? CLEAR_CUSTOMER { get; set; }
        public string? PAYER_INFO { get; set; }
        public string? ISDEL { get; set; }
        public string? IS_LOCK { get; set; }
        public string? ISEXCEL { get; set; }
        public string? EMAIL_EPAY { get; set; }
        public string? IS_CONFIRMED { get; set; }
        public string? NOTE { get; set; }
        public int? DAY_OF_PAYMENT { get; set; }
        public string? TIME_FOR_PAYMENT { get; set; }
        public string? IS_PAYMENT { get; set; }
        public string? CHIT_CD_COGS { get; set; }
        public string? DESCRIPTION_VIET { get; set; }
        public string? DESCRIPTION_ENG { get; set; }
        public string? DESCRIPTION_KOR { get; set; }
        public List<InventoryInput> INPUTS { get; set; } = new();
        public List<InventoryOutput> OUTPUTS { get; set; } = new();
    }
}
