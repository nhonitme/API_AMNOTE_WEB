namespace API_AMNOTE_WEB.Models
{
    public class BankInfo
    {
        public long BANK_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string BANK_CD { get; set; } = string.Empty;
        public string BANK_NM { get; set; } = string.Empty;
        public string? ACC_CD { get; set; }
        public string? PASSBOOK_NM { get; set; }
        public string? ACCOUNT_NUM { get; set; }
        public string? CITAD_CODE { get; set; }
        public string? REMARK { get; set; }

        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
    }

    public class BankInfoRequest
    {
        public long? BANK_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? BANK_CD { get; set; }
        public string? BANK_NM { get; set; }
        public string? ACC_CD { get; set; }
        public string? PASSBOOK_NM { get; set; }
        public string? ACCOUNT_NUM { get; set; }
        public string? CITAD_CODE { get; set; }
        public string? REMARK { get; set; }

        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
    }

    public class DeleteBankInfosRequest
    {
        public List<long> BankIds { get; set; } = new();
    }
}