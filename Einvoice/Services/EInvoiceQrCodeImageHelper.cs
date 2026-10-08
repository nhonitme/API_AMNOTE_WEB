using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using QRCoder;
namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceQrCodeImageHelper
    {
        private const string NapasGuid = "A000000727";
        private const string TaxLookupGuid = "A000000775";
        private const string DefaultMcc = "5499";

        public static string? TryExtractPayload(string xmlContent)        {
            if (string.IsNullOrWhiteSpace(xmlContent)) return null;
            try
            {
                var doc = XDocument.Parse(xmlContent, LoadOptions.None);
                var value = doc.Descendants()
                    .FirstOrDefault(x => string.Equals(x.Name.LocalName, "DLQRCode", StringComparison.OrdinalIgnoreCase))
                    ?.Value?
                    .Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                var match = Regex.Match(
                    xmlContent,
                    @"<DLQRCode[^>]*>(.*?)</DLQRCode>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!match.Success) return null;
                var value = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        public static string? TryCreatePngDataUri(string? payload, int pixelsPerModule = 6)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(payload.Trim(), QRCodeGenerator.ECCLevel.M);
                var png = new PngByteQRCode(data);
                var bytes = png.GetGraphic(Math.Clamp(pixelsPerModule, 3, 10), drawQuietZones: true);
                return "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>VietQR tĩnh từ STK + tên NH (preview designer / thiếu DLQRCode).</summary>
        public static string? TryBuildVietQrPayload(string? accountNo, string? bankNameOrBin, string? merchantName = null, string? city = null)
        {
            var account = NormalizeAccount(EInvoiceSellerMultiValueHelper.GetPrimary(accountNo));
            if (string.IsNullOrWhiteSpace(account)) return null;

            var bin = EInvoiceQrCodeSysCodeResolver.ResolveBankBin(EInvoiceSellerMultiValueHelper.GetPrimary(bankNameOrBin));
            if (string.IsNullOrWhiteSpace(bin)) return null;

            var merchantAccount =
                Tlv("00", NapasGuid)
                + Tlv("01", Tlv("00", bin) + Tlv("01", account))
                + Tlv("02", "QRIBFTTA");

            var name = TruncateAscii(merchantName, 25) ?? "SELLER";
            var cityValue = TruncateAscii(city, 15) ?? "HANOI";
            var currencyNumeric = EInvoiceQrCodeSysCodeResolver.ResolveCurrencyNumeric("VND");
            if (string.IsNullOrWhiteSpace(currencyNumeric)) return null;

            var payload =
                Tlv("00", "01")
                + Tlv("01", "11")
                + Tlv("38", merchantAccount)
                + Tlv("53", currencyNumeric)
                + Tlv("58", "VN")
                + Tlv("59", name)
                + Tlv("60", cityValue)
                + "6304";

            return payload + Crc16(payload);
        }

        /// <summary>
        /// QR HĐĐT: thanh toán VietQR (nếu có STK/NH) + tra cứu tag 99 + CRC.
        /// STK = NBan/STKNHang, số tiền = TgTTTBSo.
        /// </summary>
        public static string? TryBuildInvoiceQrPayload(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            if (invoice == null || seller == null) return null;

            var account = NormalizeAccount(EInvoiceSellerMultiValueHelper.GetPrimary(seller.STKNHANG));
            var bin = EInvoiceQrCodeSysCodeResolver.ResolveBankBin(EInvoiceSellerMultiValueHelper.GetPrimary(seller.TNHANG));
            var amountText = FormatQrAmount(invoice.TGTTTBSO);
            var hasPayment = !string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(bin);
            var currencyNumeric = hasPayment
                ? EInvoiceQrCodeSysCodeResolver.ResolveCurrencyNumeric(invoice.DVTTE)
                : null;
            if (hasPayment && string.IsNullOrWhiteSpace(currencyNumeric))
            {
                hasPayment = false;
            }
            var lookup = BuildInvoiceLookupBlock(invoice, seller);
            if (!hasPayment && string.IsNullOrWhiteSpace(lookup)) return null;

            var sb = new StringBuilder();
            sb.Append(Tlv("00", "01"));
            sb.Append(Tlv("01", hasPayment && !string.IsNullOrWhiteSpace(amountText) ? "12" : "11"));

            if (hasPayment)
            {
                var merchantAccount =
                    Tlv("00", NapasGuid)
                    + Tlv("01", Tlv("00", bin!) + Tlv("01", account!))
                    + Tlv("02", "QRIBFTTA");
                sb.Append(Tlv("38", merchantAccount));
                sb.Append(Tlv("52", DefaultMcc));
                sb.Append(Tlv("53", currencyNumeric!));
                if (!string.IsNullOrWhiteSpace(amountText))
                {
                    sb.Append(Tlv("54", Truncate(amountText!, 13)!));
                }

                sb.Append(Tlv("58", "VN"));
                sb.Append(Tlv("59", TruncateAscii(seller.SELLER_NM, 25) ?? "SELLER"));
                sb.Append(Tlv("60", "HANOI"));

                var billNumber = TruncateAscii(
                    FirstNonEmpty(invoice.SHDON, invoice.MTRACUU),
                    25);
                if (!string.IsNullOrWhiteSpace(billNumber))
                {
                    sb.Append(Tlv("62", Tlv("01", billNumber!)));
                }
            }

            if (!string.IsNullOrWhiteSpace(lookup))
            {
                sb.Append(Tlv("99", lookup!));
            }

            sb.Append("6304");
            var payloadWithoutCrc = sb.ToString();
            return payloadWithoutCrc + Crc16(payloadWithoutCrc);
        }

        private static string? BuildInvoiceLookupBlock(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            var taxCode = NormalizeTaxCode(FirstNonEmpty(seller.SELLER_TAX_CD, invoice.SELLER_TAX_CD));
            var formCode = Truncate(Common.NormalizeNullableText(invoice.KHMSHDON), 1);
            var serial = Truncate(Common.NormalizeNullableText(invoice.KHHDON), 6);
            var invoiceNo = Truncate(Common.NormalizeNullableText(invoice.SHDON), 8);
            var invoiceDate = FormatInvoiceDate(invoice.NLAP);
            var amountText = FormatQrAmount(invoice.TGTTTBSO);

            if (string.IsNullOrWhiteSpace(taxCode)
                || string.IsNullOrWhiteSpace(formCode)
                || string.IsNullOrWhiteSpace(serial)
                || string.IsNullOrWhiteSpace(invoiceNo)
                || string.IsNullOrWhiteSpace(invoiceDate)
                || string.IsNullOrWhiteSpace(amountText))
            {
                return null;
            }

            return Tlv("00", TaxLookupGuid)
                + Tlv("01", Truncate(taxCode!, 13)!)
                + Tlv("02", formCode!)
                + Tlv("03", serial!)
                + Tlv("04", invoiceNo!)
                + Tlv("05", invoiceDate!)
                + Tlv("06", Truncate(amountText!, 20)!);
        }

        private static string NormalizeAccount(string? accountNo)        {
            return Regex.Replace(accountNo ?? string.Empty, @"\s+", string.Empty);
        }

        private static string? NormalizeTaxCode(string? taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode)) return null;
            var normalized = Regex.Replace(taxCode.Trim(), @"[-\s]", string.Empty);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string? FormatQrAmount(decimal? amount)
        {
            if (amount is null) return null;
            var value = amount.Value;
            if (value < 0) value = Math.Abs(value);
            if (value == 0) return "0";

            var text = value == decimal.Truncate(value)
                ? decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string? FormatInvoiceDate(DateTime? date)
        {
            return date is null ? null : date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                var text = Common.NormalizeNullableText(value);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            return null;
        }

        private static string? TruncateAscii(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var ascii = Regex.Replace(value.Trim(), @"[^\u0020-\u007E]", string.Empty);
            return Truncate(ascii, maxLen);
        }

        private static string? Truncate(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim();
            return text.Length <= maxLen ? text : text[..maxLen];
        }

        private static string Tlv(string id, string value)
        {
            var v = value ?? string.Empty;
            return id + v.Length.ToString("00", CultureInfo.InvariantCulture) + v;
        }

        private static string Crc16(string payload)
        {
            var bytes = Encoding.ASCII.GetBytes(payload);
            var crc = 0xFFFF;
            foreach (var b in bytes)
            {
                crc ^= b << 8;
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) : (crc << 1);
                    crc &= 0xFFFF;
                }
            }

            return crc.ToString("X4", CultureInfo.InvariantCulture);
        }
    }
}
