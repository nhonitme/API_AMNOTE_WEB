namespace API_AMNOTE_WEB.Models
{
    public class AcclistInfo
    {
        public int ACC_ID { get; set; } = 0;
        public string COMPANY_CD { get; set; } = string.Empty;
        public string ACC_CD { get; set; } = string.Empty;
        public int ACC_PARENT_ID { get; set; } = 0;

        public string ACCTITLE_NM_KOR { get; set; } = string.Empty;
        public string ACCTITLE_NM_ENG { get; set; } = string.Empty;
        public string ACCTITLE_NM_VIET { get; set; } = string.Empty;
        public string ACCTITLE_NM_JAPAN { get; set; } = string.Empty;
        public string ACCTITLE_NM_CHINA { get; set; } = string.Empty;

        public string ISCUSTOMER { get; set; } = "0";
        public int ISABLETYPE { get; set; } = 1;
        public string ISABLEINPUT { get; set; } = "0";
        public string ISUSERADD { get; set; } = "0";

        public int LEVEL { get; set; } = 0;
        public string DECISION { get; set; } = string.Empty;
        public string ISDEL { get; set; } = "0";

        public string DESTINATION_ACC_CD { get; set; } = string.Empty;

        [API_AMNOTE_WEB.Helpers.BackendAudit]

        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }

        [API_AMNOTE_WEB.Helpers.BackendAudit]

        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class AcclistInfoRequest
    {
        public int ACC_ID { get; set; } = 0;
        public string ACC_CD { get; set; } = string.Empty;
        public int ACC_PARENT_ID { get; set; } = 0;

        public string ACCTITLE_NM_KOR { get; set; } = string.Empty;
        public string ACCTITLE_NM_ENG { get; set; } = string.Empty;
        public string ACCTITLE_NM_VIET { get; set; } = string.Empty;
        public string ACCTITLE_NM_JAPAN { get; set; } = string.Empty;
        public string ACCTITLE_NM_CHINA { get; set; } = string.Empty;

        public string ISCUSTOMER { get; set; } = "0";
        public int ISABLETYPE { get; set; } = 1;
        public string ISABLEINPUT { get; set; } = "0";
        public string ISUSERADD { get; set; } = "1";

        public int LEVEL { get; set; } = 0;
        public string DECISION { get; set; } = "C99";
        public string ISDEL { get; set; } = "0";
        public string DESTINATION_ACC_CD { get; set; } = string.Empty;
    }

    public class AcclistInfoID
    {
        public int ACC_ID { get; set; } = 0;        
        public string ACC_CD { get; set; } = string.Empty;
    }

    public class DeleteAcclistInfoRequest
    {
        public List<int> AcclistIds { get; set; } = new();
    }
}
