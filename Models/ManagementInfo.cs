using System;
using System.Collections.Generic;

namespace API_AMNOTE_WEB.Models
{
    public class ManagementInfo
    {
        public long MG_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string MG_CD { get; set; } = string.Empty;
        public string? MG_DESC_KOR { get; set; }
        public string? MG_DESC_ENG { get; set; }
        public string? MG_DESC_VIET { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        public string MG_CD_ROOT { get; set; } = string.Empty;
    }

    public class ManagementInfoRequest
    {
        public long? MG_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? MG_CD { get; set; }
        public string? MG_DESC_KOR { get; set; }
        public string? MG_DESC_ENG { get; set; }
        public string? MG_DESC_VIET { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        public string? MG_CD_ROOT { get; set; }
    }

    public class DeleteManagementInfosRequest
    {
        public List<long> ManagementIds { get; set; } = new();
    }
}