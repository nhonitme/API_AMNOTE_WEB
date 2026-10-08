namespace API_AMNOTE_WEB.Models
{
    public class DepartmentInfo
    {
        public long DEPARTMENT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string DEPARTMENT_CD { get; set; } = string.Empty;
        public string PARENT_CD { get; set; } = string.Empty;
        public string? DEP_NAME_KOR { get; set; }
        public string? DEP_NAME_ENG { get; set; }
        public string? DEP_NAME_VIET { get; set; }
        public string? DEP_NAME_CHINA { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_AT { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_AT { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        public string? ISDEL { get; set; }
    }

    public class DepartmentInfoRequest
    {
        public long? DEPARTMENT_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public string? PARENT_CD { get; set; }
        public string? DEP_NAME_KOR { get; set; }
        public string? DEP_NAME_ENG { get; set; }
        public string? DEP_NAME_VIET { get; set; }
        public string? DEP_NAME_CHINA { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        public string? ISDEL { get; set; }
    }

    public class DepartmentLookupItem
    {
        public string VALUE { get; set; } = string.Empty;
        public string TEXT { get; set; } = string.Empty;
    }

    public class DeleteDepartmentInfosRequest
    {
        public List<long> DepartmentIds { get; set; } = new();
    }
}
