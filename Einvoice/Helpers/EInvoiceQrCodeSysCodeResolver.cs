using System.Text.RegularExpressions;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.DependencyInjection;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    /// <summary>
    /// VietQR bank BIN aliases and ISO4217 currency codes from manager sys_code.
    /// </summary>
    internal static class EInvoiceQrCodeSysCodeResolver
    {
        public const string BankBinCodeType = "EINV_BANK_BIN";
        public const string CurrencyCodeType = "EINV_QR_CURRENCY";

        private static readonly object CacheLock = new();
        private static Dictionary<string, string>? _bankAliasToBin;
        private static Dictionary<string, string>? _currencyAlphaToNumeric;
        private static bool _cacheLoaded;

        public static IReadOnlyDictionary<string, string> BankAliasToBin
        {
            get
            {
                EnsureLoaded();
                return _bankAliasToBin ?? EmptyMap;
            }
        }

        public static IReadOnlyDictionary<string, string> CurrencyAlphaToNumeric
        {
            get
            {
                EnsureLoaded();
                return _currencyAlphaToNumeric ?? EmptyMap;
            }
        }

        private static Dictionary<string, string> EmptyMap { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public static string? ResolveBankBin(string? bankNameOrBin)
        {
            var raw = (bankNameOrBin ?? string.Empty).Trim();
            if (raw.Length == 0) return null;
            if (Regex.IsMatch(raw, @"^\d{6}$")) return raw;

            var embeddedBin = Regex.Match(raw, @"\b(97\d{4}|54\d{4}|96\d{4})\b");
            if (embeddedBin.Success) return embeddedBin.Groups[1].Value;

            var map = BankAliasToBin;
            if (map.Count == 0) return null;

            var token = Regex.Replace(raw.ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
            if (token.Length == 0) return null;

            foreach (var part in token.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (map.TryGetValue(part, out var exactBin)) return exactBin;
            }

            foreach (var pair in map.OrderByDescending(x => x.Key.Length))
            {
                if (pair.Key.Length < 3) continue;
                if (Regex.IsMatch(token, $@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        public static string? ResolveCurrencyNumeric(string? currency)
        {
            var code = (currency ?? "VND").Trim().ToUpperInvariant();
            if (Regex.IsMatch(code, @"^\d{3}$")) return code;

            var map = CurrencyAlphaToNumeric;
            return map.TryGetValue(code, out var numeric) && !string.IsNullOrWhiteSpace(numeric)
                ? numeric
                : null;
        }

        private static void EnsureLoaded()
        {
            if (_cacheLoaded) return;

            lock (CacheLock)
            {
                if (_cacheLoaded) return;
                try
                {
                    LoadCacheInternal().GetAwaiter().GetResult();
                }
                catch
                {
                    _bankAliasToBin = EmptyMap;
                    _currencyAlphaToNumeric = EmptyMap;
                }
                finally
                {
                    _cacheLoaded = true;
                }
            }
        }

        private static async Task LoadCacheInternal()
        {
            var scopeFactory = Common.ServiceProvider?.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            if (scopeFactory == null)
            {
                _bankAliasToBin = EmptyMap;
                _currencyAlphaToNumeric = EmptyMap;
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var systemService = scope.ServiceProvider.GetService<ISystemService>();
            if (systemService == null)
            {
                _bankAliasToBin = EmptyMap;
                _currencyAlphaToNumeric = EmptyMap;
                return;
            }

            var companyCd = Common.GetCompanyCode();
            var bankCodes = await systemService.GetSysCodesAsync(companyCd, BankBinCodeType);
            var currencyCodes = await systemService.GetSysCodesAsync(companyCd, CurrencyCodeType);

            _bankAliasToBin = BuildBankAliasMap(bankCodes);
            _currencyAlphaToNumeric = BuildCurrencyMap(currencyCodes);
        }

        private static Dictionary<string, string> BuildBankAliasMap(IEnumerable<SysCodeInfo> codes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in codes)
            {
                if (!IsActive(item)) continue;

                var bin = Common.NormalizeNullableText(item.CODE_CD);
                var alias = NormalizeAlias(item.CODE_NAME);
                if (string.IsNullOrWhiteSpace(bin) || string.IsNullOrWhiteSpace(alias)) continue;

                map.TryAdd(alias, bin);
            }

            return map;
        }

        private static Dictionary<string, string> BuildCurrencyMap(IEnumerable<SysCodeInfo> codes)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in codes)
            {
                if (!IsActive(item)) continue;

                var numeric = Common.NormalizeNullableText(item.CODE_CD);
                var alpha = NormalizeAlias(item.CODE_NAME);
                if (string.IsNullOrWhiteSpace(numeric) || string.IsNullOrWhiteSpace(alpha)) continue;

                map.TryAdd(alpha, numeric);
            }

            return map;
        }

        private static bool IsActive(SysCodeInfo item)
            => item.IS_ACTIVE is null or 1
               && !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeAlias(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return Regex.Replace(value.Trim().ToUpperInvariant(), @"[^A-Z0-9]+", string.Empty);
        }
    }
}
