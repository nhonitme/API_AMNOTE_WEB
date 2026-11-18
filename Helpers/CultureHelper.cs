using System.Globalization;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Culture helpers that stay safe when the host runs in globalization-invariant mode
    /// (common on Linux self-contained publish), where vi-VN is unavailable.
    /// </summary>
    public static class CultureHelper
    {
        public static CultureInfo Vietnamese { get; } = CreateVietnameseCulture();

        private static CultureInfo CreateVietnameseCulture()
        {
            try
            {
                return CultureInfo.GetCultureInfo("vi-VN");
            }
            catch (CultureNotFoundException)
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NumberGroupSeparator = ".";
                culture.NumberFormat.NumberDecimalSeparator = ",";
                culture.NumberFormat.CurrencyGroupSeparator = ".";
                culture.NumberFormat.CurrencyDecimalSeparator = ",";
                return CultureInfo.ReadOnly(culture);
            }
        }

        public static CultureInfo TryGet(string name, CultureInfo fallback)
        {
            try
            {
                return CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                return fallback;
            }
        }
    }
}
