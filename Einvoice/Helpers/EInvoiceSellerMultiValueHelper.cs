using System.Xml.Linq;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    /// <summary>
    /// Người bán: SĐT / STK / NH có thể nhập nhiều, cách nhau ';'.
    /// XML: giá trị đầu → thẻ chuẩn; các giá trị còn lại → NBan/TTKhac.
    /// </summary>
    public static class EInvoiceSellerMultiValueHelper
    {
        public const string PhoneField = "SDThoai";
        public const string AccountField = "STKNHang";
        public const string BankField = "TNHang";

        public static IReadOnlyList<string> ParseSemiList(string? value)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Array.Empty<string>();
            }

            return normalized
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();
        }

        public static string? GetPrimary(string? value)
        {
            return ParseSemiList(value).FirstOrDefault();
        }

        public static IReadOnlyList<string> GetExtras(string? value)
        {
            var parts = ParseSemiList(value);
            return parts.Count <= 1 ? Array.Empty<string>() : parts.Skip(1).ToList();
        }

        public static XElement? BuildSellerContactTtKhac(string? phones, string? accounts, string? banks)
        {
            var children = new List<XElement>();

            foreach (var phone in GetExtras(phones))
            {
                children.Add(BuildInfoRow(PhoneField, phone));
            }

            var extraAccounts = GetExtras(accounts);
            var extraBanks = GetExtras(banks);
            var pairCount = Math.Max(extraAccounts.Count, extraBanks.Count);
            for (var i = 0; i < pairCount; i++)
            {
                if (i < extraAccounts.Count)
                {
                    children.Add(BuildInfoRow(AccountField, extraAccounts[i]));
                }

                if (i < extraBanks.Count)
                {
                    children.Add(BuildInfoRow(BankField, extraBanks[i]));
                }
            }

            return children.Count == 0 ? null : new XElement("TTKhac", children);
        }

        private static XElement BuildInfoRow(string fieldName, string value)
        {
            return new XElement("TTin",
                new XElement("TTruong", fieldName),
                new XElement("KDLieu", "string"),
                new XElement("DLieu", value));
        }
    }
}
