namespace API_AMNOTE_WEB.Services
{
    public class AmnoteAiOptions
    {
        public const string SectionName = "AmnoteAi";

        /// <summary>Base URL only, e.g. http://ai.amnote.com.vn</summary>
        public string BaseUrl { get; set; } = "http://ai.amnote.com.vn";

        /// <summary>X-Api-Key. Prefer env AmnoteAi__ApiKey — do not commit secrets.</summary>
        public string ApiKey { get; set; } = string.Empty;

        public string ResolveBaseUrl()
        {
            var baseUrl = (BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            return string.IsNullOrWhiteSpace(baseUrl) ? "http://ai.amnote.com.vn" : baseUrl;
        }

        public string ResolveNapDuLieuAutoUrl() => $"{ResolveBaseUrl()}/nap-du-lieu-auto";
        public string ResolveGoiYHangLoatUrl() => $"{ResolveBaseUrl()}/goi-y-hang-loat";
        public string ResolveDichThuatUrl() => $"{ResolveBaseUrl()}/dich-thuat";
        public string ResolveHocTapUrl() => $"{ResolveBaseUrl()}/hoc-tap";
        public string ResolveGoiYUrl() => $"{ResolveBaseUrl()}/goi-y";
        public string ResolvePhanTichHoaDonUrl() => $"{ResolveBaseUrl()}/phan-tich-hoa-don";
        public string ResolveThemRuleMoiCtyUrl() => $"{ResolveBaseUrl()}/them-rule-moi-cty";
        public string ResolveThemOverrideRuleCtyUrl() => $"{ResolveBaseUrl()}/them-override-rule-cty";
        public string ResolveToggleRuleGlobalCtyUrl() => $"{ResolveBaseUrl()}/toggle-rule-global-cty";
        public string ResolveXoaOverrideRuleCtyUrl() => $"{ResolveBaseUrl()}/xoa-override-rule-cty";
        public string ResolveXoaRuleMoiCtyUrl() => $"{ResolveBaseUrl()}/xoa-rule-moi-cty";
    }
}
