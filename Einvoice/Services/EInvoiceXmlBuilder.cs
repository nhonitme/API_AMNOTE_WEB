using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal sealed class EInvoiceXmlBuilder
    {
        private static readonly string[] LookupInfoFieldNames =
        {
            "MaTraCuu",
            "MTRACUU",
            "Mã tra cứu",
            "Ma tra cuu",
            "Mã tra cứu hóa đơn",
            "Ma tra cuu hoa don"
        };
        private const string DetailAfterTaxLabel = "Thành tiền thanh toán của hàng hóa";
        private const string DetailTaxAmountLabel = "Tiền thuế dòng (Tiền thuế GTGT)";
        private const string CommercialDiscountDescription = "Chiết khấu thương mại";
        private const int CommercialDiscountTchat = 3;

        private readonly EInvoiceDecimalFormatter _formatter;
        private readonly string _currencyCode;
        private readonly bool _isWithoutTaxRate;
        private readonly bool _isWarehouseForm;

        public EInvoiceXmlBuilder(EInvoiceDecimalFormatter formatter, string? currencyCode, string? khmsHDON = null)
        {
            _formatter = formatter;
            _currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "VND" : currencyCode.Trim().ToUpperInvariant();
            _isWarehouseForm = EInvoiceWarehouseHelper.IsWarehouseForm(khmsHDON);
            _isWithoutTaxRate = EInvoiceTaxCalculator.IsWithoutTaxRate(khmsHDON);
        }

        public string Build(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            var qrPayload = ResolveDlQrCode(invoice, seller);
            var hdonChildren = _isWarehouseForm
                ? new XElement?[]
                {
                    BuildDlHDon(invoice, seller),
                    TextElement("DLQRCode", qrPayload),
                    BuildSignatureSection(),
                }
                : new XElement?[]
                {
                    BuildDlHDon(invoice, seller),
                    TextElement("DLQRCode", qrPayload),
                    TextElement("MCCQT", invoice.MCCQT),
                    BuildSignatureSection(),
                };

            var document = new XDocument(
                BuildElement("HDon", hdonChildren) ?? new XElement("HDon")
            );

            return XmlSerializationHelper.Serialize(document);
        }

        /// <summary>Ưu tiên payload QR tự tạo từ STK/NH + TgTTTBSo + chỉ tiêu HĐ; fallback DLQRCODE sẵn có.</summary>
        private static string? ResolveDlQrCode(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            var generated = EInvoiceQrCodeImageHelper.TryBuildInvoiceQrPayload(invoice, seller);
            if (!string.IsNullOrWhiteSpace(generated))
            {
                invoice.DLQRCODE = generated;
                return generated;
            }

            return Common.NormalizeNullableText(invoice.DLQRCODE);
        }

        private XElement BuildDlHDon(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            var detailElements = invoice.DETAILS
                .Where(x => x.ISDEL != 1)
                .OrderBy(x => x.STT ?? int.MaxValue)
                .Select(BuildDetail)
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            AppendSyntheticCommercialDiscountDetailIfNeeded(invoice, detailElements);

            var element = _isWarehouseForm
                ? BuildWarehouseDlHDon(invoice, seller, detailElements)
                : _isWithoutTaxRate
                    ? BuildWithoutTaxDlHDon(invoice, seller, detailElements)
                    : BuildStandardDlHDon(invoice, seller, detailElements);

            var dlhDonId = FormatValue(invoice.MTDIEP);
            if (!string.IsNullOrWhiteSpace(dlhDonId))
            {
                element.SetAttributeValue("Id", dlhDonId);
            }

            return element;
        }

        private XElement BuildStandardDlHDon(EInvoiceInfo invoice, EInvoiceSellerInfo seller, List<XElement> detailElements)
        {
            return BuildElement("DLHDon",
                BuildElement("TTChung",
                    TextElement("PBan", invoice.PBAN),
                    TextElement("THDon", invoice.THDON),
                    TextElement("KHMSHDon", invoice.KHMSHDON),
                    TextElement("KHHDon", invoice.KHHDON),
                    TextElement("SHDon", invoice.SHDON),
                    TextElement("MHSo", invoice.MHSO),
                    TextElement("NLap", invoice.NLAP),
                    TextElement("HDCTTChinh", invoice.HDCTTCHINH),
                    TextElement("SBKe", invoice.SBKE),
                    TextElement("NBKe", invoice.NBKE),
                    TextElement("DVTTe", invoice.DVTTE),
                    BuildTgiaHeaderElement(invoice.DVTTE, invoice.TGIA),
                    TextElement("HTTToan", invoice.HTTTOAN),
                    TextElement("MSTTCGP", invoice.MSTTCGP),
                    BuildRelatedInvoice(invoice.RELATED),
                    BuildHeaderAdditionalInfo(invoice)),
                BuildElement("NDHDon",
                    BuildSeller(invoice, seller),
                    BuildBuyer(invoice),
                    detailElements.Count > 0 ? new XElement("DSHHDVu", detailElements) : null,
                    BuildPayment(invoice))) ?? new XElement("DLHDon");
        }

        private XElement BuildWithoutTaxDlHDon(EInvoiceInfo invoice, EInvoiceSellerInfo seller, List<XElement> detailElements)
        {
            return BuildElement("DLHDon",
                BuildElement("TTChung",
                    TextElement("PBan", invoice.PBAN),
                    TextElement("THDon", invoice.THDON),
                    TextElement("KHMSHDon", invoice.KHMSHDON),
                    TextElement("KHHDon", invoice.KHHDON),
                    TextElement("SHDon", invoice.SHDON),
                    TextElement("MHSo", invoice.MHSO),
                    TextElement("NLap", invoice.NLAP),
                    TextElement("HDDCKPTQuan", invoice.HDCTTCHINH),
                    TextElement("SBKe", invoice.SBKE),
                    TextElement("NBKe", invoice.NBKE),
                    TextElement("DVTTe", invoice.DVTTE),
                    BuildTgiaHeaderElement(invoice.DVTTE, invoice.TGIA),
                    TextElement("HTTToan", invoice.HTTTOAN),
                    TextElement("MSTTCGP", invoice.MSTTCGP),
                    BuildRelatedInvoice(invoice.RELATED),
                    BuildHeaderAdditionalInfo(invoice)),
                BuildElement("NDHDon",
                    BuildSeller(invoice, seller),
                    BuildBuyer(invoice),
                    detailElements.Count > 0 ? new XElement("DSHHDVu", detailElements) : null,
                    BuildPayment(invoice))) ?? new XElement("DLHDon");
        }

        private XElement BuildWarehouseDlHDon(EInvoiceInfo invoice, EInvoiceSellerInfo seller, List<XElement> detailElements)
        {
            var warehouseFields = EInvoiceWarehouseHelper.ReadFields(invoice.PXK_INFO, invoice.EXTRA_JSON);
            var variant = EInvoiceWarehouseHelper.ResolveWarehouseVariant(invoice.KHHDON);

            return BuildElement("DLHDon",
                BuildElement("TTChung",
                    TextElement("PBan", invoice.PBAN),
                    TextElement("THDon", invoice.THDON),
                    TextElement("KHMSHDon", invoice.KHMSHDON),
                    TextElement("KHHDon", invoice.KHHDON),
                    TextElement("SHDon", invoice.SHDON),
                    TextElement("NLap", invoice.NLAP),
                    TextElement("DVTTe", invoice.DVTTE),
                    BuildTgiaHeaderElement(invoice.DVTTE, invoice.TGIA),
                    TextElement("MSTTCGP", invoice.MSTTCGP)),
                BuildElement("NDHDon",
                    BuildWarehouseSeller(invoice, seller, warehouseFields, variant),
                    BuildWarehouseBuyer(invoice, variant),
                    detailElements.Count > 0 ? new XElement("DSHHDVu", detailElements) : null)) ?? new XElement("DLHDon");
        }

        private XElement? BuildWarehouseSeller(
            EInvoiceInfo invoice,
            EInvoiceSellerInfo seller,
            EInvoiceWarehouseFields warehouseFields,
            char? variant)
        {
            var exportAddress = FormatValue(warehouseFields.NbanDChi);
            if (string.IsNullOrWhiteSpace(exportAddress))
            {
                exportAddress = FormatValue(seller.SELLER_ADDRESS);
            }

            if (variant == 'B')
            {
                return BuildElement("NBan",
                    TextElement("Ten", seller.SELLER_NM),
                    TextElement("MST", seller.SELLER_TAX_CD),
                    TextElement("HDKTSo", warehouseFields.HdktSo),
                    TextElement("HDKTNgay", warehouseFields.HdktNgay),
                    TextElement("DChi", exportAddress),
                    TextElement("HVTNXHang", warehouseFields.HvtnxHang),
                    TextElement("TNVChuyen", warehouseFields.TnvChuyen),
                    TextElement("HDSo", warehouseFields.HdSo),
                    TextElement("PTVChuyen", warehouseFields.PtvChuyen));
            }

            return BuildElement("NBan",
                TextElement("Ten", seller.SELLER_NM),
                TextElement("MST", seller.SELLER_TAX_CD),
                TextElement("LDDNBo", warehouseFields.LddnBo),
                TextElement("DChi", exportAddress),
                TextElement("HDSo", warehouseFields.HdSo),
                TextElement("HVTNXHang", warehouseFields.HvtnxHang),
                TextElement("TNVChuyen", warehouseFields.TnvChuyen),
                TextElement("PTVChuyen", warehouseFields.PtvChuyen));
        }

        private XElement? BuildWarehouseBuyer(EInvoiceInfo invoice, char? variant)
        {
            if (variant == 'B')
            {
                return BuildElement("NMua",
                    TextElement("Ten", invoice.NMUA_TEN),
                    TextElement("MST", invoice.NMUA_MST),
                    TextElement("HVTNNHang", invoice.NMUA_HVTNMHANG),
                    TextElement("DChi", invoice.NMUA_DCHI));
            }

            return BuildElement("NMua",
                TextElement("Ten", invoice.NMUA_TEN),
                TextElement("MST", invoice.NMUA_MST),
                TextElement("DChi", invoice.NMUA_DCHI),
                TextElement("HVTNNHang", invoice.NMUA_HVTNMHANG));
        }

        private XElement? BuildSeller(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            var isHouseholdBusiness = EInvoiceSellerTaxCodeHelper.IsHouseholdBusinessTaxCode(seller.SELLER_TAX_CD);
            return BuildElement("NBan",
                TextElement("Ten", seller.SELLER_NM),
                TextElement("MST", seller.SELLER_TAX_CD),
                TextElement("DChi", seller.SELLER_ADDRESS),
                isHouseholdBusiness ? TextElement("MDDKDoanh", seller.MDDKDOANH) : null,
                isHouseholdBusiness ? TextElement("TDDKDoanh", seller.TDDKDOANH) : null,
                isHouseholdBusiness ? TextElement("DCDDKDoanh", seller.DCDDKDOANH) : null,
                TextElement("MCHang", seller.MCHANG),
                TextElement("TCHang", seller.TCHANG),
                TextElement("SDThoai", EInvoiceSellerMultiValueHelper.GetPrimary(seller.SDTHOAI)),
                TextElement("DCTDTu", seller.DCTDTU),
                TextElement("STKNHang", EInvoiceSellerMultiValueHelper.GetPrimary(seller.STKNHANG)),
                TextElement("TNHang", EInvoiceSellerMultiValueHelper.GetPrimary(seller.TNHANG)),
                TextElement("Fax", seller.FAX),
                TextElement("Website", seller.WEBSITE),
                EInvoiceSellerMultiValueHelper.BuildSellerContactTtKhac(seller.SDTHOAI, seller.STKNHANG, seller.TNHANG));
        }

        private XElement? BuildBuyer(EInvoiceInfo invoice)
        {
            return BuildElement("NMua",
                TextElement("Ten", invoice.NMUA_TEN),
                TextElement("MST", invoice.NMUA_MST),
                TextElement("MDVQHNSach", invoice.NMUA_MDVQHNSACH),
                TextElement("DChi", invoice.NMUA_DCHI),
                TextElement("MTinh", invoice.NMUA_MTINH),
                TextElement("TTinh", invoice.NMUA_TTINH),
                TextElement("MXa", invoice.NMUA_MXA),
                TextElement("TXa", invoice.NMUA_TXA),
                TextElement("MKHang", invoice.NMUA_MKHANG),
                TextElement("SDThoai", invoice.NMUA_SDTHOAI),
                TextElement("CCCDan", invoice.NMUA_CCCDAN),
                TextElement("SHChieu", invoice.NMUA_SHCHIEU),
                TextElement("DCTDTu", EInvoiceBuyerEmailHelper.GetPrimaryBuyerEmailForXml(invoice.NMUA_DCTDTU)),
                TextElement("HVTNMHang", invoice.NMUA_HVTNMHANG),
                TextElement("STKNHang", invoice.NMUA_STKNHANG),
                TextElement("TNHang", invoice.NMUA_TNHANG),
                BuildAdditionalInfo(null));
        }

        private XElement? BuildVatReductionPaymentTtkhac(EInvoiceInfo invoice)
        {
            return EInvoiceVatReductionHelper.BuildPaymentTtkhac(invoice, _formatter, Common.GetCurrentLanguage());
        }

        private XElement? BuildPayment(EInvoiceInfo invoice)
        {
            if (_isWithoutTaxRate && !_isWarehouseForm)
            {
                var paymentChildren = new List<XElement?>
                {
                    BuildFees(invoice.FEE_JSON),
                    DecimalHeaderElement("TTCKTMai", "TTCKTMAI", invoice.TTCKTMAI),
                };

                if (EInvoiceVatReductionHelper.IsActive(invoice))
                {
                    paymentChildren.Add(DecimalHeaderElement("TgTCThue", "TGTCTHUE", invoice.TGTCTHUE));
                }

                paymentChildren.Add(DecimalHeaderElement("TGTKhac", "TGTKHAC", invoice.TGTKHAC));
                paymentChildren.Add(DecimalHeaderElement("TgTTTBSo", "TGTTTBSO", invoice.TGTTTBSO));
                paymentChildren.Add(TextElement("TgTTTBChu", invoice.TGTTTBCHU));
                paymentChildren.Add(BuildVatReductionPaymentTtkhac(invoice));

                return BuildElement("TToan", paymentChildren.ToArray());
            }

            return BuildElement("TToan",
                BuildTaxSummary(invoice),
                DecimalHeaderElement("TgTCThue", "TGTCTHUE", invoice.TGTCTHUE),
                DecimalHeaderElement("TGTKCThue", "TGTKCTHUE", invoice.TGTKCTHUE),
                DecimalHeaderElement("TgTThue", "TGTTTHUE", invoice.TGTTTHUE),
                BuildFees(invoice.FEE_JSON),
                DecimalHeaderElement("TTCKTMai", "TTCKTMAI", invoice.TTCKTMAI),
                DecimalHeaderElement("TGTKhac", "TGTKHAC", invoice.TGTKHAC),
                DecimalHeaderElement("TgTTTBSo", "TGTTTBSO", invoice.TGTTTBSO),
                TextElement("TgTTTBChu", invoice.TGTTTBCHU),
                BuildAdditionalInfo(null));
        }

        private XElement BuildSignatureSection()
        {
            return new XElement("DSCKS",
                new XElement("NBan", new XElement("Signature"))
            );
        }

        private XElement? BuildDetail(EInvoiceDetail detail)
        {
            var detailAmount = ResolveDetailAmountForXml(detail);
            if (_isWarehouseForm)
            {
                var warehouseElement = BuildElement("HHDVu",
                    TextElement("TChat", detail.TCHAT),
                    TextElement("STT", detail.STT),
                    TextElement("MHHDVu", detail.MHHDVU),
                    TextElement("THHDVu", detail.THHDVU),
                    TextElement("DVTinh", detail.DVTINH),
                    DecimalDetailElement("SLuong", "SLUONG", detail.SLUONG),
                    DecimalDetailElement("DGia", "DGIA", detail.DGIA),
                    DecimalDetailElement("ThTien", "THTIEN", detailAmount));

                if (warehouseElement == null)
                {
                    return null;
                }

                AddIfNotNull(warehouseElement, BuildDetailAdditionalInfo(detail));
                return warehouseElement;
            }

            var children = _isWithoutTaxRate
                ? new XElement?[]
                {
                    TextElement("TChat", detail.TCHAT),
                    TextElement("STT", detail.STT),
                    TextElement("MHHDVu", detail.MHHDVU),
                    TextElement("THHDVu", detail.THHDVU),
                    TextElement("DVTinh", detail.DVTINH),
                    DecimalDetailElement("SLuong", "SLUONG", detail.SLUONG),
                    DecimalDetailElement("DGia", "DGIA", detail.DGIA),
                    DecimalDetailElement("TLCKhau", "TLCKHAU", detail.TLCKHAU),
                    DecimalDetailElement("STCKhau", "STCKHAU", detail.STCKHAU),
                    DecimalDetailElement("ThTien", "THTIEN", detailAmount),
                }
                : new XElement?[]
                {
                    TextElement("STT", detail.STT),
                    TextElement("TChat", detail.TCHAT),
                    TextElement("MHHDVu", detail.MHHDVU),
                    TextElement("THHDVu", detail.THHDVU),
                    TextElement("DVTinh", detail.DVTINH),
                    DecimalDetailElement("SLuong", "SLUONG", detail.SLUONG),
                    DecimalDetailElement("DGia", "DGIA", detail.DGIA),
                    DecimalDetailElement("TLCKhau", "TLCKHAU", detail.TLCKHAU),
                    DecimalDetailElement("STCKhau", "STCKHAU", detail.STCKHAU),
                    DecimalDetailElement("ThTien", "THTIEN", detailAmount),
                    TextElement("TSuat", detail.TSUAT),
                };

            var element = BuildElement("HHDVu", children);

            if (element == null)
            {
                return null;
            }

            if (detail.TCHAT == 5)
            {
                var specialInfo = BuildSpecialInfo(detail.SPECIAL);
                if (specialInfo != null)
                {
                    element.Add(specialInfo);
                }
            }

            AddIfNotNull(element, BuildDetailAdditionalInfo(detail));
            return element;
        }

        private static decimal? ResolveDetailAmountForXml(EInvoiceDetail detail)
        {
            if (detail.TCHAT == 3 && detail.THTIEN.HasValue)
            {
                return Math.Abs(detail.THTIEN.Value);
            }

            return detail.THTIEN;
        }

        private void AppendSyntheticCommercialDiscountDetailIfNeeded(EInvoiceInfo invoice, List<XElement> detailElements)
        {
            if (_isWarehouseForm)
            {
                return;
            }

            var discountAmount = invoice.TTCKTMAI;
            if (discountAmount is null or 0m)
            {
                return;
            }

            var hasDiscountDetail = invoice.DETAILS.Any(x => x.ISDEL != 1 && x.TCHAT == CommercialDiscountTchat);
            if (hasDiscountDetail)
            {
                return;
            }

            var syntheticDetail = BuildSyntheticCommercialDiscountDetail(invoice, discountAmount.Value);
            if (syntheticDetail != null)
            {
                detailElements.Add(syntheticDetail);
            }
        }

        private XElement? BuildSyntheticCommercialDiscountDetail(EInvoiceInfo invoice, decimal amount)
        {
            var detail = new EInvoiceDetail
            {
                TCHAT = CommercialDiscountTchat,
                THHDVU = ResolveCommercialDiscountDescription(invoice),
                THTIEN = amount,
                TSUAT = ResolveCommercialDiscountTaxRate(invoice),
            };

            return BuildDetail(detail);
        }

        private static string ResolveCommercialDiscountDescription(EInvoiceInfo invoice)
        {
            var note = Common.NormalizeNullableText(invoice.CKTMAI_GCHU);
            return !string.IsNullOrWhiteSpace(note) ? note : CommercialDiscountDescription;
        }

        private string? ResolveCommercialDiscountTaxRate(EInvoiceInfo invoice)
        {
            var fromDetails = invoice.DETAILS
                .Where(x => x.ISDEL != 1 && x.TCHAT != CommercialDiscountTchat)
                .Select(x => Common.NormalizeNullableText(x.TSUAT))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (!string.IsNullOrWhiteSpace(fromDetails))
            {
                return fromDetails;
            }

            return ReadInfoRows(invoice.TAX_SUMMARY_JSON)
                .Select(row => Common.NormalizeNullableText(row.TaxRate ?? row.Name))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private bool ShouldIncludeRelatedInvoice(EInvoiceRelatedInfo? related)
        {
            if (related == null)
            {
                return false;
            }

            return related.TCHDON is 1 or 2 or 3 or 4;
        }

        private XElement? BuildRelatedInvoice(EInvoiceRelatedInfo? related)
        {
            if (!ShouldIncludeRelatedInvoice(related))
            {
                return null;
            }

            return BuildElement("TTHDLQuan",
                TextElement("TCHDon", related.TCHDON),
                TextElement("MSTCLQuan", related.MSTCLQUAN),
                TextElement("LHDCLQuan", related.LHDCLQUAN),
                TextElement("KHMSHDCLQuan", related.KHMSHDCLQUAN),
                TextElement("KHHDCLQuan", related.KHHDCLQUAN),
                TextElement("SHDCLQuan", related.SHDCLQUAN),
                TextElement("NLHDCLQuan", related.NLHDCLQUAN),
                TextElement("LDDCTThe", related.LDDCTTHE),
                TextElement("GChu", related.GCHU),
                TextElement("SBKCLQuan", related.SBKCLQUAN),
                TextElement("NBKCLQuan", related.NBKCLQUAN));
        }

        private XElement? BuildHeaderAdditionalInfo(EInvoiceInfo invoice)
        {
            var additionalInfo = BuildAdditionalInfo(invoice.EXTRA_JSON);
            var lookupCode = FormatValue(invoice.MTRACUU);
            if (string.IsNullOrWhiteSpace(lookupCode))
            {
                return additionalInfo;
            }

            additionalInfo ??= new XElement("TTKhac");
            RemoveLookupInfoRows(additionalInfo);
            additionalInfo.Add(BuildLookupInfo(lookupCode));

            return additionalInfo.HasElements ? additionalInfo : null;
        }

        private XElement BuildLookupInfo(string lookupCode)
        {
            return new XElement("TTin",
                new XElement("TTruong", "MaTraCuu"),
                new XElement("KDLieu", "string"),
                new XElement("DLieu", lookupCode));
        }

        private void RemoveLookupInfoRows(XElement additionalInfo)
        {
            foreach (var element in additionalInfo.Elements().ToList())
            {
                if (string.Equals(element.Name.LocalName, "MaTraCuu", StringComparison.OrdinalIgnoreCase) ||
                    IsLookupInfoRow(element))
                {
                    element.Remove();
                }
            }
        }

        private bool IsLookupInfoRow(XElement element)
        {
            if (!string.Equals(element.Name.LocalName, "TTin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fieldName = element.Elements()
                .FirstOrDefault(child => string.Equals(child.Name.LocalName, "TTruong", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return IsLookupInfoFieldName(fieldName);
        }

        private bool IsLookupInfoFieldName(string? fieldName)
        {
            var normalizedFieldName = Common.NormalizeNullableText(fieldName);
            return !string.IsNullOrWhiteSpace(normalizedFieldName) &&
                LookupInfoFieldNames.Any(name => string.Equals(name, normalizedFieldName, StringComparison.OrdinalIgnoreCase));
        }

        private XElement? BuildSpecialInfo(EInvoiceDetailSpecialInfo? special)
        {
            if (special == null || special.LHHDTRUNG is not (1 or 2 or 3 or 4))
            {
                return null;
            }

            var rows = new List<(string Name, string? Value)>();
            switch (special.LHHDTRUNG)
            {
                case 1:
                    rows.Add(("SKhung", special.SKHUNG));
                    rows.Add(("SMay", special.SMAY));
                    break;
                case 2:
                    rows.Add(("BKSPTVChuyen", special.BKSPT_VCHUYEN));
                    break;
                case 3:
                    rows.Add(("TNGHang", special.TNG_HANG));
                    rows.Add(("DCNGHang", special.DCNG_HANG));
                    rows.Add(("MSTNGHang", special.MSTNG_HANG));
                    rows.Add(("MDDNGHang", special.MDDNG_HANG));
                    break;
            }

            foreach (var extra in ReadSpecialExtraRows(special.EXTRA_JSON))
            {
                if (rows.Any(x => string.Equals(x.Name, extra.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                rows.Add(extra);
            }

            var children = rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Value))
                .Select(row => BuildElement("TTin",
                    TextElement("DLieu", row.Value),
                    TextElement("LHHDTrung", special.LHHDTRUNG),
                    TextElement("TTruong", row.Name)))
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            if (children.Count == 0)
            {
                return null;
            }

            return new XElement("TTHHDTrung", children);
        }

        private static IEnumerable<(string Name, string? Value)> ReadSpecialExtraRows(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                yield break;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                yield break;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    yield break;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var name = property.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.True => "1",
                        JsonValueKind.False => "0",
                        _ => property.Value.ToString()
                    };

                    yield return (name, value);
                }
            }
        }

        private XElement? BuildAdditionalInfo(string? json)
        {
            var xml = ReadAdditionalInfoXml(json);
            if (xml != null)
            {
                return xml;
            }

            var rows = ReadInfoRows(json).ToList();
            var children = rows
                .Select(row => BuildElement("TTin",
                    TextElement("TTruong", row.Name),
                    TextElement("DLieu", row.Value)))
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            if (children.Count == 0)
            {
                return null;
            }

            return new XElement("TTKhac", children);
        }

        private XElement? BuildDetailAdditionalInfo(EInvoiceDetail detail)
        {
            var additionalInfo = BuildAdditionalInfo(detail.EXTRA_JSON);
            var taxInfoRows = BuildDetailTaxInfoRows(detail).ToList();
            if (taxInfoRows.Count == 0)
            {
                return additionalInfo;
            }

            additionalInfo ??= new XElement("TTKhac");
            RemoveDetailTaxInfoRows(additionalInfo);
            additionalInfo.Add(taxInfoRows);

            return additionalInfo.HasElements ? additionalInfo : null;
        }

        private IEnumerable<XElement> BuildDetailTaxInfoRows(EInvoiceDetail detail)
        {
            if (_isWithoutTaxRate || _isWarehouseForm)
            {
                yield break;
            }

            var isCommercialDiscount = detail.TCHAT == 3;
            var afterTaxAmount = isCommercialDiscount && detail.TSAUTHUE.HasValue
                ? Math.Abs(detail.TSAUTHUE.Value)
                : detail.TSAUTHUE;
            var taxAmount = isCommercialDiscount && detail.TTHUE.HasValue
                ? Math.Abs(detail.TTHUE.Value)
                : detail.TTHUE;

            var afterTaxText = _formatter.FormatDetail("TSAUTHUE", afterTaxAmount, _currencyCode);
            if (!string.IsNullOrWhiteSpace(afterTaxText))
            {
                yield return BuildDetailInfoRow(DetailAfterTaxLabel, afterTaxText);
            }

            var taxText = _formatter.FormatDetail("TTHUE", taxAmount, _currencyCode);
            if (!string.IsNullOrWhiteSpace(taxText))
            {
                yield return BuildDetailInfoRow(DetailTaxAmountLabel, taxText);
            }
        }

        private static XElement BuildDetailInfoRow(string fieldName, string value)
        {
            return new XElement("TTin",
                new XElement("DLieu", value),
                new XElement("KDLieu", "string"),
                new XElement("TTruong", fieldName));
        }

        private static void RemoveDetailTaxInfoRows(XElement additionalInfo)
        {
            foreach (var element in additionalInfo.Elements().ToList())
            {
                if (!string.Equals(element.Name.LocalName, "TTin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldName = element.Elements()
                    .FirstOrDefault(child => string.Equals(child.Name.LocalName, "TTruong", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (IsDetailTaxInfoFieldName(fieldName))
                {
                    element.Remove();
                }
            }
        }

        private static bool IsDetailTaxInfoFieldName(string? fieldName)
        {
            var normalizedFieldName = Common.NormalizeNullableText(fieldName);
            return string.Equals(normalizedFieldName, DetailAfterTaxLabel, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedFieldName, DetailTaxAmountLabel, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedFieldName, "TSAUTHUE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedFieldName, "TTHUE", StringComparison.OrdinalIgnoreCase);
        }

        private XElement? ReadAdditionalInfoXml(string? value)
        {
            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('<'))
            {
                return null;
            }

            try
            {
                var element = XElement.Parse(text, LoadOptions.PreserveWhitespace);
                if (string.Equals(element.Name.LocalName, "TTKhac", StringComparison.OrdinalIgnoreCase))
                {
                    var children = element.Elements()
                        .Select(child => new XElement(child))
                        .Where(child => !string.IsNullOrWhiteSpace(child.Value) || child.HasElements)
                        .ToList();

                    return children.Count == 0 ? null : new XElement("TTKhac", children);
                }

                if (string.Equals(element.Name.LocalName, "TTin", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(element.Value) && !element.HasElements)
                    {
                        return null;
                    }

                    return new XElement("TTKhac", new XElement(element));
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private XElement? BuildTaxSummary(EInvoiceInfo invoice)
        {
            var rows = ReadInfoRows(invoice.TAX_SUMMARY_JSON).ToList();
            if (rows.Count == 0)
            {
                var fallbackTaxRate = invoice.DETAILS
                    .Where(x => x.ISDEL != 1)
                    .Select(x => x.TSUAT)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (string.IsNullOrWhiteSpace(fallbackTaxRate) &&
                    invoice.TGTCTHUE == null &&
                    invoice.TGTTTHUE == null)
                {
                    return null;
                }

                rows.Add(new InfoRow
                {
                    TaxRate = fallbackTaxRate,
                    Amount = invoice.TGTCTHUE,
                    TaxAmount = invoice.TGTTTHUE
                });
            }

            return BuildTaxSummaryElement(rows);
        }

        private XElement? BuildTaxSummaryElement(IEnumerable<InfoRow> rows)
        {
            var children = rows
                .Select(row => BuildElement("LTSuat",
                    TextElement("TSuat", row.TaxRate ?? row.Name),
                    DecimalDetailElement("ThTien", "THTIEN", row.Amount ?? ParseDecimal(row.Value)),
                    DecimalHeaderElement("TThue", "TGTTTHUE", row.TaxAmount)))
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            if (children.Count == 0)
            {
                return null;
            }

            return new XElement("THTTLTSuat", children);
        }

        private XElement? BuildFees(string? json)
        {
            var rows = ReadInfoRows(json).ToList();
            var children = rows
                .Select(row => BuildElement("LPhi",
                    TextElement("TLPhi", row.Name),
                    TextElement("TPhi", row.Amount ?? ParseDecimal(row.Value))))
                .Where(element => element != null)
                .Cast<XElement>()
                .ToList();

            if (children.Count == 0)
            {
                return null;
            }

            return new XElement("DSLPhi", children);
        }

        private XElement? DecimalDetailElement(string name, string fieldKey, decimal? value)
        {
            var text = _formatter.FormatDetail(fieldKey, value, _currencyCode);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return new XElement(name, text);
        }

        private XElement BuildTgiaHeaderElement(string? dvtte, decimal? tgia)
        {
            var currencyCode = string.IsNullOrWhiteSpace(dvtte) ? _currencyCode : dvtte.Trim().ToUpperInvariant();
            var isForeignCurrency = !string.Equals(currencyCode, "VND", StringComparison.OrdinalIgnoreCase);
            var rate = isForeignCurrency ? tgia.GetValueOrDefault() : 1m;
            if (rate <= 0m)
            {
                rate = 1m;
            }

            var text = _formatter.FormatHeader("TGIA", rate, currencyCode);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = rate.ToString(CultureInfo.InvariantCulture);
            }

            return new XElement("TGia", text);
        }

        private XElement? DecimalHeaderElement(string name, string fieldKey, decimal? value)
        {
            var text = _formatter.FormatHeader(fieldKey, value, _currencyCode);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return new XElement(name, text);
        }

        private XElement? TextElement(string name, object? value)
        {
            var text = FormatValue(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return new XElement(name, text);
        }

        private XElement? BuildElement(string name, params XElement?[] children)
        {
            return BuildElement(name, children.AsEnumerable());
        }

        private XElement? BuildElement(string name, IEnumerable<XElement?>? children)
        {
            if (children == null)
            {
                return null;
            }

            var elements = children.Where(child => child != null).Cast<XElement>().ToList();
            if (elements.Count == 0)
            {
                return null;
            }

            return new XElement(name, elements);
        }

        private void AddIfNotNull(XElement parent, XElement? child)
        {
            if (child != null)
            {
                parent.Add(child);
            }
        }

        private string FormatValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime date => date == DateTime.MinValue ? string.Empty : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                decimal number => number.ToString("0.######", CultureInfo.InvariantCulture),
                double number => number.ToString("0.######", CultureInfo.InvariantCulture),
                float number => number.ToString("0.######", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString()?.Trim() ?? string.Empty
            };
        }

        private IReadOnlyDictionary<string, JsonElement> ReadObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                }

                return document.RootElement.EnumerateObject()
                    .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private IEnumerable<InfoRow> ReadInfoRows(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                yield break;
            }

            using var document = TryParseJson(json);
            if (document == null)
            {
                yield break;
            }

            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "TTin", out var infoElement))
            {
                root = infoElement;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var row = ReadInfoRow(item);
                    if (!row.IsEmpty)
                    {
                        yield return row;
                    }
                }

                yield break;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var objectRow = ReadInfoRow(root);
                if (!objectRow.IsEmpty)
                {
                    yield return objectRow;
                    yield break;
                }

                foreach (var property in root.EnumerateObject())
                {
                    var value = JsonValueText(property.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return new InfoRow { Name = property.Name, Value = value };
                    }
                }
            }
        }

        private JsonDocument? TryParseJson(string json)
        {
            try
            {
                return JsonDocument.Parse(json);
            }
            catch
            {
                return null;
            }
        }

        private InfoRow ReadInfoRow(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return new InfoRow { Value = JsonValueText(element) };
            }

            return new InfoRow
            {
                Type = ReadValue(element, "LHHDTrung", "Loai", "Type"),
                Name = ReadValue(element, "TTruong", "TLPhi", "TSuat", "Name", "Key"),
                Value = ReadValue(element, "DLieu", "Value", "GiaTri"),
                TaxRate = ReadValue(element, "TSuat", "TaxRate"),
                Amount = ParseDecimal(ReadValue(element, "ThTien", "TPhi", "Amount")),
                TaxAmount = ParseDecimal(ReadValue(element, "TThue", "TaxAmount"))
            };
        }

        private string? ReadValue(IReadOnlyDictionary<string, JsonElement> values, string key)
        {
            return values.TryGetValue(key, out var value) ? JsonValueText(value) : null;
        }

        private string? ReadValue(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (TryGetProperty(element, key, out var value))
                {
                    return JsonValueText(value);
                }
            }

            return null;
        }

        private bool TryGetProperty(JsonElement element, string key, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private string? JsonValueText(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetDecimal(out var number) ? FormatValue(number) : value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.GetRawText()
            };
        }

        private decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        private sealed class InfoRow
        {
            public string? Type { get; init; }
            public string? Name { get; init; }
            public string? Value { get; init; }
            public string? TaxRate { get; init; }
            public decimal? Amount { get; init; }
            public decimal? TaxAmount { get; init; }

            public bool IsEmpty =>
                string.IsNullOrWhiteSpace(Type) &&
                string.IsNullOrWhiteSpace(Name) &&
                string.IsNullOrWhiteSpace(Value) &&
                string.IsNullOrWhiteSpace(TaxRate) &&
                Amount == null &&
                TaxAmount == null;
        }
    }
}
