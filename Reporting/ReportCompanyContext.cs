using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Reports
{
    public sealed class CompanyReportInfo
    {
        public string COMPANY_CD { get; init; } = string.Empty;
        public int? DB_GROUP_ID { get; init; }
        public string? COMPANY_NM { get; init; }
        public string? COMPANY_NM_EN { get; init; }
        public string? COMPANY_NM_KOR { get; init; }
        public short? COMPANY_TYPE { get; init; }
        public short? COMPANY_KIND { get; init; }
        public string? DE_COMPANY_CD { get; init; }
        public string? COMPANY_LV { get; init; }
        public string? ACCDATE_CD { get; init; }
        public string? TAX_CD { get; init; }
        public string? CCCDan { get; init; }
        public string? TCQTQLy { get; init; }
        public string? MCQTQLy { get; init; }
        public string? BRN { get; init; }
        public string? CRN { get; init; }
        public string? OWNER_NM { get; init; }
        public string? ZIP_CODE { get; init; }
        public string? ADDRESS_DO { get; init; }
        public string? ADDRESS { get; init; }
        public string? ADDRESS_ENG { get; init; }
        public string? ADDRESS_KOR { get; init; }
        public string? CARRYFORWARD_YMD { get; init; }
        public string? SIDO { get; init; }
        public string? GUMYUN { get; init; }
        public string? BUSINESS_TYPE { get; init; }
        public string? KIND_BUSINESS { get; init; }
        public string? TEL { get; init; }
        public string? EMAIL { get; init; }
        public string? WEBSITE { get; init; }
        public string? FAX { get; init; }
        public string? STOCKCALC_TYPE { get; init; }
        public string? OPEN_YMD { get; init; }
        public string? DECISION { get; init; }
        public DateTime? REG_YMD { get; init; }
        public string ISDEL { get; init; } = "0";
        public string? NOTE { get; init; }
    }

    public sealed class CompanyReportSignatureInfo
    {
        public long ID { get; init; }
        public string COMPANY_CD { get; init; } = string.Empty;
        public string SIGN_CODE { get; init; } = string.Empty;
        public string? DISPLAY_LABEL { get; init; }
        public string? SIGN_NAME { get; init; }
        public string? SIGN_TITLE { get; init; }
        public string? SIGN_IMAGE_URL { get; init; }
        public int? SORT_ORDER { get; init; }
        public int? SIGN_SEQUENCE { get; init; }
    }

    public sealed class ReportCompanyContext
    {
        public CompanyReportInfo CompanyInfo { get; init; } = new CompanyReportInfo();
        public IReadOnlyList<CompanyReportSignatureInfo> Signatures { get; init; } = Array.Empty<CompanyReportSignatureInfo>();
        public string ReportLanguage { get; init; } = "VIET";
        public string CompanyNameText { get; init; } = "-";
        public string CompanyDisplayText { get; init; } = "-";
        public string PrintDateText { get; init; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string AddressDisplayText { get; init; } = "-";
    }

    internal static class ReportCompanyContextFactory
    {
        public static ReportCompanyContext Create(
            CompanyInfo? companyInfo,
            IEnumerable<CompanyReportSignatureInfo>? signatures,
            string companyCd,
            string? reportLanguage)
        {

            var normalizedCompanyCd = Normalize(companyCd) ?? string.Empty;
            var normalizedReportLanguage = ReportLanguageHelper.NormalizeLanguage(reportLanguage);
            var resolvedCompanyInfo = MapCompanyInfo(companyInfo, normalizedCompanyCd);
            var resolvedSignatures = MapSignatures(signatures);
            var companyNameText = BuildCompanyNameText(resolvedCompanyInfo, normalizedReportLanguage);
            var addressDisplayText = BuildAddressDisplayText(resolvedCompanyInfo, normalizedReportLanguage);

            return new ReportCompanyContext
            {
                CompanyInfo = resolvedCompanyInfo,
                Signatures = resolvedSignatures,
                ReportLanguage = normalizedReportLanguage,
                CompanyNameText = companyNameText,
                CompanyDisplayText = BuildCompanyDisplayText(companyNameText, resolvedCompanyInfo.COMPANY_CD),
                PrintDateText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AddressDisplayText = addressDisplayText
            };
        }

        private static CompanyReportInfo MapCompanyInfo(CompanyInfo? companyInfo, string fallbackCompanyCd)
        {
            return new CompanyReportInfo
            {
                COMPANY_CD = FirstNonEmpty(companyInfo?.COMPANY_CD, fallbackCompanyCd) ?? string.Empty,
                DB_GROUP_ID = companyInfo?.DB_GROUP_ID,
                COMPANY_NM = Normalize(companyInfo?.COMPANY_NM),
                COMPANY_NM_EN = Normalize(companyInfo?.COMPANY_NM_EN),
                COMPANY_NM_KOR = Normalize(companyInfo?.COMPANY_NM_KOR),
                COMPANY_TYPE = companyInfo?.COMPANY_TYPE,
                COMPANY_KIND = companyInfo?.COMPANY_KIND,
                DE_COMPANY_CD = Normalize(companyInfo?.DE_COMPANY_CD),
                COMPANY_LV = Normalize(companyInfo?.COMPANY_LV),
                ACCDATE_CD = Normalize(companyInfo?.ACCDATE_CD),
                TAX_CD = Normalize(companyInfo?.TAX_CD),
                CCCDan = Normalize(companyInfo?.CCCDan),
                TCQTQLy = Normalize(companyInfo?.TCQTQLy),
                MCQTQLy = Normalize(companyInfo?.MCQTQLy),
                BRN = Normalize(companyInfo?.BRN),
                CRN = Normalize(companyInfo?.CRN),
                OWNER_NM = Normalize(companyInfo?.OWNER_NM),
                ZIP_CODE = Normalize(companyInfo?.ZIP_CODE),
                ADDRESS_DO = Normalize(companyInfo?.ADDRESS_DO),
                ADDRESS = Normalize(companyInfo?.ADDRESS),
                ADDRESS_ENG = Normalize(companyInfo?.ADDRESS_ENG),
                ADDRESS_KOR = Normalize(companyInfo?.ADDRESS_KOR),
                CARRYFORWARD_YMD = Normalize(companyInfo?.CARRYFORWARD_YMD),
                SIDO = Normalize(companyInfo?.SIDO),
                GUMYUN = Normalize(companyInfo?.GUMYUN),
                BUSINESS_TYPE = Normalize(companyInfo?.BUSINESS_TYPE),
                KIND_BUSINESS = Normalize(companyInfo?.KIND_BUSINESS),
                TEL = Normalize(companyInfo?.TEL),
                EMAIL = Normalize(companyInfo?.EMAIL),
                WEBSITE = Normalize(companyInfo?.WEBSITE),
                FAX = Normalize(companyInfo?.FAX),
                STOCKCALC_TYPE = Normalize(companyInfo?.STOCKCALC_TYPE),
                OPEN_YMD = Normalize(companyInfo?.OPEN_YMD),
                DECISION = Normalize(companyInfo?.DECISION),
                REG_YMD = companyInfo?.REG_YMD,
                ISDEL = FirstNonEmpty(companyInfo?.ISDEL, "0") ?? "0",
                NOTE = Normalize(companyInfo?.NOTE)
            };
        }

        private static IReadOnlyList<CompanyReportSignatureInfo> MapSignatures(IEnumerable<CompanyReportSignatureInfo>? signatures)
        {
            return (signatures ?? Array.Empty<CompanyReportSignatureInfo>())
                .Select(item => new CompanyReportSignatureInfo
                {
                    ID = item.ID,
                    COMPANY_CD = Normalize(item.COMPANY_CD) ?? string.Empty,
                    SIGN_CODE = Normalize(item.SIGN_CODE) ?? string.Empty,
                    DISPLAY_LABEL = Normalize(item.DISPLAY_LABEL) ?? string.Empty,// không translate() ở đây nha anh
                    SIGN_NAME = Normalize(item.SIGN_NAME) ?? string.Empty,
                    SIGN_TITLE = Normalize(item.SIGN_TITLE) ?? string.Empty,
                    SIGN_IMAGE_URL = Normalize(item.SIGN_IMAGE_URL) ?? string.Empty,
                    SORT_ORDER = item.SORT_ORDER,
                    SIGN_SEQUENCE = item.SIGN_SEQUENCE
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.SIGN_CODE))
                .OrderBy(item => item.SIGN_SEQUENCE ?? int.MaxValue)
                .ThenBy(item => item.SORT_ORDER ?? int.MaxValue)
                .ThenBy(item => item.SIGN_CODE, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildCompanyDisplayText(string companyName, string? companyCd)
        {
            if (!string.IsNullOrWhiteSpace(companyName) && !string.IsNullOrWhiteSpace(companyCd))
            {
                return $"{companyName} ({companyCd})";
            }

            return FirstNonEmpty(companyName, companyCd) ?? "-";
        }

        private static string BuildCompanyNameText(CompanyReportInfo companyInfo, string reportLanguage)
        {
            return reportLanguage switch
            {
                "ENG" => FirstNonEmpty(companyInfo.COMPANY_NM_EN, companyInfo.COMPANY_NM, companyInfo.COMPANY_NM_KOR, companyInfo.COMPANY_CD) ?? "-",
                "KOR" => FirstNonEmpty(companyInfo.COMPANY_NM_KOR, companyInfo.COMPANY_NM, companyInfo.COMPANY_NM_EN, companyInfo.COMPANY_CD) ?? "-",
                "CHN" or "THA" => FirstNonEmpty(companyInfo.COMPANY_NM, companyInfo.COMPANY_NM_EN, companyInfo.COMPANY_NM_KOR, companyInfo.COMPANY_CD) ?? "-",
                _ => FirstNonEmpty(companyInfo.COMPANY_NM, companyInfo.COMPANY_NM_EN, companyInfo.COMPANY_NM_KOR, companyInfo.COMPANY_CD) ?? "-"
            };
        }

        private static string BuildAddressDisplayText(CompanyReportInfo companyInfo, string reportLanguage)
        {
            return reportLanguage switch
            {
                "ENG" => FirstNonEmpty(companyInfo.ADDRESS_ENG, companyInfo.ADDRESS, companyInfo.ADDRESS_KOR) ?? "-",
                "KOR" => FirstNonEmpty(companyInfo.ADDRESS_KOR, companyInfo.ADDRESS, companyInfo.ADDRESS_ENG) ?? "-",
                "CHN" or "THA" => FirstNonEmpty(companyInfo.ADDRESS, companyInfo.ADDRESS_ENG, companyInfo.ADDRESS_KOR) ?? "-",
                _ => FirstNonEmpty(companyInfo.ADDRESS, companyInfo.ADDRESS_ENG, companyInfo.ADDRESS_KOR) ?? "-"
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                var normalized = Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
