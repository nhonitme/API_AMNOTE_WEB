namespace API_AMNOTE_WEB.Models
{
    public class SysCodeSequence
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string OBJECT_TYPE { get; set; } = string.Empty;
        public string MENU_CODE { get; set; } = string.Empty;
        public string CODE_FIELD { get; set; } = string.Empty;
        public string PREFIX { get; set; } = string.Empty;
        public string SUFFIX { get; set; } = string.Empty;
        public string CODE_PATTERN { get; set; } = "{PREFIX}{NO}{SUFFIX}";
        public long CURRENT_NO { get; set; }
        public int NUMBER_LENGTH { get; set; }
        public string RESET_TYPE { get; set; } = string.Empty;
        public string RESET_KEY { get; set; } = string.Empty;
        public string IS_USE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class SysCodeSequenceRequest
    {
        public long? ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? OBJECT_TYPE { get; set; }
        public string? MENU_CODE { get; set; }
        public string? CODE_FIELD { get; set; }
        public string? PREFIX { get; set; }
        public string? SUFFIX { get; set; }
        public string? CODE_PATTERN { get; set; }
        public long? CURRENT_NO { get; set; }
        public int? NUMBER_LENGTH { get; set; }
        public string? RESET_TYPE { get; set; }
        public string? RESET_KEY { get; set; }
        public string? IS_USE { get; set; }
    }

    public class SysCodeSequencePreviewRequest
    {
        public string? COMPANY_CD { get; set; }
        public string? OBJECT_TYPE { get; set; }
        public DateTime? BASE_DATE { get; set; }
    }

    public class SysCodeSequencePreview
    {
        public string OBJECT_TYPE { get; set; } = string.Empty;
        public string MENU_CODE { get; set; } = string.Empty;
        public string CODE_FIELD { get; set; } = string.Empty;
        public string? NEXT_CD { get; set; }
        public string PREFIX { get; set; } = string.Empty;
        public string SUFFIX { get; set; } = string.Empty;
        public string CODE_PATTERN { get; set; } = "{PREFIX}{NO}{SUFFIX}";
        public long CURRENT_NO { get; set; }
        public long NEXT_NO { get; set; }
        public int NUMBER_LENGTH { get; set; }
        public string RESET_TYPE { get; set; } = string.Empty;
        public string RESET_KEY { get; set; } = string.Empty;
    }

    public class SysCodeSequenceResolveResult
    {
        public string? GENERATED_CD { get; set; }
        public long CURRENT_NO { get; set; }
        public string WAS_SEQUENCE_USED { get; set; } = "0";
        public string WAS_CURRENT_NO_UPDATED { get; set; } = "0";
    }
}
