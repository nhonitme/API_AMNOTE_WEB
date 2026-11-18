namespace API_AMNOTE_WEB.Models.DTOs
{
    public class AiLicenseStatusDto
    {
        public bool HasLicense { get; set; }
        public bool IsActive { get; set; }
        public bool IsInited { get; set; }
        public bool CanInit { get; set; }
        public bool CanUseAi { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? StopMsg { get; set; }
        public int UserLimit { get; set; }
    }

    public class AiInitStepResultDto
    {
        public string Step { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class AiInitResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AiInitStepResultDto> Steps { get; set; } = new();
        public AiLicenseStatusDto? Status { get; set; }
    }

    public class AiSuggestAccountItemRequest
    {
        public string LineKey { get; set; } = string.Empty;
        public string TenHang { get; set; } = string.Empty;
        public string? TenKh { get; set; }
        public string? LoaiHoaDon { get; set; }
    }

    public class AiSuggestAccountsRequest
    {
        public List<AiSuggestAccountItemRequest> Items { get; set; } = new();
    }

    public class AiSuggestAccountItemResult
    {
        public string LineKey { get; set; } = string.Empty;
        public string? Debit { get; set; }
        public string? Credit { get; set; }
        public string? Reason { get; set; }
    }

    public class AiSuggestAccountsResponse
    {
        public List<AiSuggestAccountItemResult> Items { get; set; } = new();
    }

    public class AiTranslateRequest
    {
        public string? Text { get; set; }
        public string? TargetLang { get; set; }
        public List<AiTranslateItemRequest>? Items { get; set; }
    }

    public class AiTranslateItemRequest
    {
        public string? Key { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? TargetLang { get; set; }
    }

    public class AiTranslateItemResult
    {
        public string? Key { get; set; }
        public string Text { get; set; } = string.Empty;
        public string TargetLang { get; set; } = "ENG";
        public string Result { get; set; } = string.Empty;
    }

    public class AiTranslateResponse
    {
        public string? Text { get; set; }
        public string? TargetLang { get; set; }
        public string? Result { get; set; }
        public List<AiTranslateItemResult> Items { get; set; } = new();
    }

    public class AiPostingRuleListItemDto
    {
        public long RuleId { get; set; }
        public string RuleCd { get; set; } = string.Empty;
        public string RuleNm { get; set; } = string.Empty;
        public string RuleScope { get; set; } = string.Empty;
        public string RuleLevel { get; set; } = string.Empty;
        public string BusinessType { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool IsDefault { get; set; }
    }

    public class AiPostingRuleConditionDto
    {
        public long ConditionId { get; set; }
        public string FieldNm { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string? Value1 { get; set; }
        public string? Value2 { get; set; }
        public string MatchCaseYn { get; set; } = "0";
        public string RequiredYn { get; set; } = "0";
        public double Score { get; set; }
        public int Sort { get; set; }
        public string? Description { get; set; }
    }

    public class AiPostingRuleActionDto
    {
        public long ActionId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? DebitAccCd { get; set; }
        public string? CreditAccCd { get; set; }
        public int Sort { get; set; }
        public string? Description { get; set; }
    }

    public class AiPostingRuleDetailDto
    {
        public long RuleId { get; set; }
        public string RuleCd { get; set; } = string.Empty;
        public string RuleNm { get; set; } = string.Empty;
        public string RuleScope { get; set; } = "PURCHASE";
        public string RuleLevel { get; set; } = "LINE";
        public string BusinessType { get; set; } = "SERVICE";
        public int Priority { get; set; } = 10;
        public double ConfidenceMin { get; set; } = 90;
        public string ProcessInventoryFlag { get; set; } = "0";
        public string NeedAssetProfileFlag { get; set; } = "0";
        public string? Note { get; set; }
        public bool IsDefault { get; set; }
        public List<AiPostingRuleConditionDto> Conditions { get; set; } = new();
        public List<AiPostingRuleActionDto> Actions { get; set; } = new();
    }
}
