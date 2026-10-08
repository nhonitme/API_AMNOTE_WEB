using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal sealed class EInvoiceSellerPreviewOptions
    {
        public string CurrencyCode { get; init; } = "VND";
        public EInvoiceDecimalFormatter? Formatter { get; init; }
        /// <summary>
        /// true = decimal demo only (NMua + dòng hàng + thanh toán).
        /// false = template/designer layout (NBan + chữ ký nháp; không người mua/hàng hóa).
        /// </summary>
        public bool IncludeDemoContent { get; init; } = true;
    }

    internal static class EInvoiceSellerPreviewXmlBuilder
    {
        /// <summary>Số HĐ preview — Mau2 nhận diện watermark HÓA ĐƠN NHÁP + form e-sign giả.</summary>
        private const string PreviewShdon = "0000000";
        private const string DemoForeignCurrency = "USD";
        private const decimal DemoExchangeRate = 25_000m;

        public static async Task<string> BuildAsync(EInvoiceSellerInfo seller, EInvoiceSellerPreviewOptions? options = null)
        {
            var currencyCode = NormalizeCurrencyCode(options?.CurrencyCode);
            var formatter = options?.Formatter ?? EInvoiceDecimalFormatter.Create([]);
            var isForeign = !string.Equals(currencyCode, "VND", StringComparison.OrdinalIgnoreCase);
            var includeDemo = options?.IncludeDemoContent ?? true;

            var invoiceDate = DateTime.Today.ToString("yyyy-MM-dd");
            var thdon = await EInvoiceSellerThdonResolver.ResolveAsync(seller.KHMSHDON, seller.KHHDON);
            if (string.IsNullOrWhiteSpace(thdon))
            {
                thdon = Common.NormalizeNullableText(seller.THDON) ?? string.Empty;
            }

            var qrPayload = EInvoiceQrCodeImageHelper.TryBuildVietQrPayload(
                EInvoiceSellerMultiValueHelper.GetPrimary(seller.STKNHANG),
                EInvoiceSellerMultiValueHelper.GetPrimary(seller.TNHANG),
                seller.SELLER_NM);

            XElement? buyer = includeDemo ? BuildBuyer() : null;
            XElement? details = includeDemo ? BuildDemoDetails(formatter, currencyCode, isForeign) : null;
            XElement? payment = includeDemo ? BuildDemoPayment(formatter, currencyCode, isForeign) : null;

            var document = new XDocument(
                new XElement("HDon",
                    new XElement("DLHDon",
                        new XAttribute("Id", $"DLHDON-PREVIEW-{invoiceDate.Replace("-", string.Empty)}"),
                        new XElement("TTChung",
                            new XElement("PBan", "2.1.0"),
                            FillElement("THDon", thdon),
                            FillElement("KHMSHDon", seller.KHMSHDON),
                            FillElement("KHHDon", seller.KHHDON),
                            new XElement("SHDon", PreviewShdon),
                            new XElement("NLap", invoiceDate),
                            new XElement("DVTTe", currencyCode),
                            isForeign && includeDemo
                                ? new XElement("TGia", formatter.FormatHeader("TGIA", DemoExchangeRate, currencyCode))
                                : null,
                            includeDemo ? new XElement("HTTToan", "TM/CK") : null),
                        new XElement("NDHDon",
                            BuildSeller(seller),
                            buyer,
                            details,
                            payment)),
                    string.IsNullOrWhiteSpace(qrPayload) ? null : new XElement("DLQRCode", qrPayload),
                    BuildSignatureSection()));

            return XmlSerializationHelper.Serialize(document);
        }

        /// <summary>Chưa ký thật (Signature rỗng) để watermark nháp hiện; form e-sign vẫn hiện nhờ SHDon preview.</summary>
        private static XElement BuildSignatureSection()
        {
            return new XElement("DSCKS",
                new XElement("NBan"));
        }

        private static XElement BuildSeller(EInvoiceSellerInfo seller)
        {
            var isHouseholdBusiness = EInvoiceSellerTaxCodeHelper.IsHouseholdBusinessTaxCode(seller.SELLER_TAX_CD);
            var element = new XElement("NBan",
                FillElement("Ten", seller.SELLER_NM),
                FillElement("MST", seller.SELLER_TAX_CD),
                FillElement("DChi", seller.SELLER_ADDRESS),
                isHouseholdBusiness ? FillElement("MDDKDoanh", seller.MDDKDOANH) : null,
                isHouseholdBusiness ? FillElement("TDDKDoanh", seller.TDDKDOANH) : null,
                isHouseholdBusiness ? FillElement("DCDDKDoanh", seller.DCDDKDOANH) : null,
                FillElement("MCHang", seller.MCHANG),
                FillElement("TCHang", seller.TCHANG),
                FillElement("SDThoai", EInvoiceSellerMultiValueHelper.GetPrimary(seller.SDTHOAI)),
                FillElement("DCTDTu", seller.DCTDTU),
                FillElement("STKNHang", EInvoiceSellerMultiValueHelper.GetPrimary(seller.STKNHANG)),
                FillElement("TNHang", EInvoiceSellerMultiValueHelper.GetPrimary(seller.TNHANG)),
                FillElement("Fax", seller.FAX),
                FillElement("Website", seller.WEBSITE),
                EInvoiceSellerMultiValueHelper.BuildSellerContactTtKhac(seller.SDTHOAI, seller.STKNHANG, seller.TNHANG));

            return element;
        }

        private static XElement BuildBuyer()
        {
            return new XElement("NMua",
                new XElement("Ten", "Công ty TNHH Demo Khách Hàng"),
                new XElement("MST", "0312345678"),
                new XElement("DChi", "123 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh"),
                new XElement("HVTNMHang", "Nguyễn Văn A"),
                new XElement("SDThoai", "0901234567"),
                new XElement("DCTDTu", "demo@example.com"));
        }

        private static XElement BuildDemoDetails(EInvoiceDecimalFormatter formatter, string currencyCode, bool isForeign)
        {
            if (isForeign)
            {
                return new XElement("DSHHDVu",
                    BuildDemoDetail(
                        formatter,
                        currencyCode,
                        stt: 1,
                        code: "SP001",
                        name: "Software consulting package (demo)",
                        unit: "Pkg",
                        quantity: 2.5m,
                        unitPrice: 52.5675m,
                        discountRate: 5m,
                        discountAmount: 6.5709m,
                        amount: 124.8478m,
                        taxRate: "10%"),
                    BuildDemoDetail(
                        formatter,
                        currencyCode,
                        stt: 2,
                        code: "SP002",
                        name: "Implementation fee (demo)",
                        unit: "Time",
                        quantity: 1m,
                        unitPrice: 140m,
                        discountRate: 0m,
                        discountAmount: 0m,
                        amount: 140m,
                        taxRate: "10%"));
            }

            return new XElement("DSHHDVu",
                BuildDemoDetail(
                    formatter,
                    currencyCode,
                    stt: 1,
                    code: "SP001",
                    name: "Dịch vụ tư vấn phần mềm kế toán (demo)",
                    unit: "Gói",
                    quantity: 2.5m,
                    unitPrice: 1_250_000.567m,
                    discountRate: 5m,
                    discountAmount: 156_250.071m,
                    amount: 2_968_751.347m,
                    taxRate: "10%"),
                BuildDemoDetail(
                    formatter,
                    currencyCode,
                    stt: 2,
                    code: "SP002",
                    name: "Phí triển khai hệ thống (demo)",
                    unit: "Lần",
                    quantity: 1m,
                    unitPrice: 3_500_000m,
                    discountRate: 0m,
                    discountAmount: 0m,
                    amount: 3_500_000m,
                    taxRate: "10%"));
        }

        private static XElement BuildDemoDetail(
            EInvoiceDecimalFormatter formatter,
            string currencyCode,
            int stt,
            string code,
            string name,
            string unit,
            decimal quantity,
            decimal unitPrice,
            decimal discountRate,
            decimal discountAmount,
            decimal amount,
            string taxRate)
        {
            return new XElement("HHDVu",
                new XElement("TChat", "1"),
                new XElement("STT", stt.ToString(CultureInfo.InvariantCulture)),
                new XElement("MHHDVu", code),
                new XElement("THHDVu", name),
                new XElement("DVTinh", unit),
                new XElement("SLuong", formatter.FormatDetail("SLUONG", quantity, currencyCode)),
                new XElement("DGia", formatter.FormatDetail("DGIA", unitPrice, currencyCode)),
                new XElement("TLCKhau", formatter.FormatDetail("TLCKHAU", discountRate, currencyCode)),
                new XElement("STCKhau", formatter.FormatDetail("STCKHAU", discountAmount, currencyCode)),
                new XElement("ThTien", formatter.FormatDetail("THTIEN", amount, currencyCode)),
                new XElement("TSuat", taxRate));
        }

        private static XElement BuildDemoPayment(EInvoiceDecimalFormatter formatter, string currencyCode, bool isForeign)
        {
            if (isForeign)
            {
                // 124.8478 + 140 = 264.8478 before tax
                // VAT 10% = 26.4848
                // Total = 291.3326
                return new XElement("TToan",
                    new XElement("THTTLTSuat",
                        new XElement("LTSuat",
                            new XElement("TSuat", "10%"),
                            new XElement("ThTien", formatter.FormatDetail("THTIEN", 264.8478m, currencyCode)),
                            new XElement("TThue", formatter.FormatHeader("TGTTTHUE", 26.4848m, currencyCode)))),
                    new XElement("TgTCThue", formatter.FormatHeader("TGTCTHUE", 264.8478m, currencyCode)),
                    new XElement("TgTThue", formatter.FormatHeader("TGTTTHUE", 26.4848m, currencyCode)),
                    new XElement("TTCKTMai", formatter.FormatHeader("TTCKTMAI", 6.5709m, currencyCode)),
                    new XElement("TgTTTBSo", formatter.FormatHeader("TGTTTBSO", 291.3326m, currencyCode)),
                    new XElement("TgTTTBChu", "Hai trăm chín mươi mốt đô la Mỹ ba mươi ba xu"));
            }

            // 2,968,751.347 + 3,500,000 = 6,468,751.347 before tax
            // VAT 10% = 646,875.135
            // Total = 7,115,626.482
            return new XElement("TToan",
                new XElement("THTTLTSuat",
                    new XElement("LTSuat",
                        new XElement("TSuat", "10%"),
                        new XElement("ThTien", formatter.FormatDetail("THTIEN", 6_468_751.347m, currencyCode)),
                        new XElement("TThue", formatter.FormatHeader("TGTTTHUE", 646_875.135m, currencyCode)))),
                new XElement("TgTCThue", formatter.FormatHeader("TGTCTHUE", 6_468_751.347m, currencyCode)),
                new XElement("TgTThue", formatter.FormatHeader("TGTTTHUE", 646_875.135m, currencyCode)),
                new XElement("TTCKTMai", formatter.FormatHeader("TTCKTMAI", 156_250.071m, currencyCode)),
                new XElement("TgTTTBSo", formatter.FormatHeader("TGTTTBSO", 7_115_626.482m, currencyCode)),
                new XElement("TgTTTBChu", "Bảy triệu một trăm mười lăm nghìn sáu trăm hai mươi sáu đồng"));
        }

        private static string NormalizeCurrencyCode(string? currencyCode)
        {
            var normalized = (currencyCode ?? "VND").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || normalized is "VND" or "ANY")
            {
                return "VND";
            }

            if (normalized is "FC" or "NGOAITE" or "FOREIGN")
            {
                return DemoForeignCurrency;
            }

            return normalized;
        }

        private static XElement? FillElement(string name, string? value)
        {
            var text = Common.NormalizeNullableText(value);
            return string.IsNullOrWhiteSpace(text) ? null : new XElement(name, text);
        }
    }
}
