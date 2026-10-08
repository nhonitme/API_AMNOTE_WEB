using System.Globalization;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    /// <summary>
    /// Derive THDON from mẫu số / ký hiệu.
    /// Titles are loaded from manager sys_code CODE_TYPE = EINV_THDON.
    /// </summary>
    internal static class EInvoiceSellerThdonResolver
    {
        public const string CodeType = "EINV_THDON";

        private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

        public static Task<string> ResolveAsync(string? khmsHdon, string? khhdon)
            => ResolveAsync(khmsHdon, khhdon, Common.GetCompanyCode());

        public static async Task<string> ResolveAsync(string? khmsHdon, string? khhdon, string? companyCd)
        {
            var codeCd = ResolveTitleCode(khmsHdon, khhdon);
            if (string.IsNullOrWhiteSpace(codeCd))
            {
                return string.Empty;
            }

            var title = await LookupTitleAsync(companyCd, codeCd);
            return string.IsNullOrWhiteSpace(title) ? string.Empty : ToStoredTitle(title);
        }

        /// <summary>Maps mẫu số + ký hiệu → CODE_CD of EINV_THDON.</summary>
        public static string ResolveTitleCode(string? khmsHdon, string? khhdon)
        {
            var khms = NormalizeKhmsHdon(khmsHdon);
            var khhd = NormalizeKhhdon(khhdon);
            if (khms.Length == 0 || khhd.Length < 4)
            {
                return string.Empty;
            }

            var typeLetter = khhd[3];

            if (khms == "7" || typeLetter == 'X')
            {
                return "COMMERCIAL";
            }

            if (typeLetter == 'N')
            {
                return "PXK_N";
            }

            if (typeLetter == 'B')
            {
                return "PXK_B";
            }

            if (typeLetter == 'L')
            {
                return khms == "2" ? "SALES" : "VAT";
            }

            if (typeLetter == 'M')
            {
                return khms == "2" ? "SALES_MTT" : "VAT_MTT";
            }

            return khms switch
            {
                "1" => "VAT",
                "2" => "SALES",
                _ => string.Empty,
            };
        }

        public static string NormalizeKhmsHdon(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public static string NormalizeKhhdon(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpper(VietnameseCulture);

        private static async Task<string?> LookupTitleAsync(string? companyCd, string codeCd)
        {
            try
            {
                var resolvedCompanyCd = string.IsNullOrWhiteSpace(companyCd)
                    ? Common.GetCompanyCode()
                    : companyCd.Trim();

                var scopeFactory = Common.ServiceProvider?.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
                if (scopeFactory == null)
                {
                    return null;
                }

                using var scope = scopeFactory.CreateScope();
                var systemService = scope.ServiceProvider.GetService<ISystemService>();
                if (systemService == null)
                {
                    return null;
                }

                var codes = await systemService.GetSysCodesAsync(resolvedCompanyCd, CodeType);
                var match = codes.FirstOrDefault(item =>
                    string.Equals(Common.NormalizeNullableText(item.CODE_CD), codeCd, StringComparison.OrdinalIgnoreCase)
                    && (item.IS_ACTIVE is null or 1)
                    && !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase));

                return Common.NormalizeNullableText(match?.CODE_NAME);
            }
            catch
            {
                return null;
            }
        }

        private static string ToStoredTitle(string title)
            => title.ToUpper(VietnameseCulture);
    }
}
