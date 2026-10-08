using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Transmission;
using System.Globalization;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services;

public partial class EInvoiceService
{
    private sealed class MttBatchRow
    {
        public string BATCH_ID { get; set; } = "";
        public string RAW_XML { get; set; } = "";
        public string ITEMS_JSON { get; set; } = "";
        public string? SIGNED_XML { get; set; }
        public DateTime NLAP { get; set; }
    }
    private sealed record MttItem(long Id, long SellerId, int Version, string Form, string Series, string Number, string? Code);
    private sealed class MttIssuedRow
    {
        public string? SHDON { get; set; }
        public string? MCCQT { get; set; }
        public int IS_SIGNED { get; set; }
    }
    private sealed class MttRegistration
    {
        public string? MCCQT { get; set; }
        public int CMTMTTIEN { get; set; }
        public int KCMTMTTIEN { get; set; }
    }

    private static DateTime VietnamToday() => DateTime.UtcNow.AddHours(7).Date;

    private static bool IsMttIssued(EInvoiceInfo invoice) => HasText(invoice.SHDON);

    private static string ResolveMttMessageType(string? series) =>
        series?.StartsWith("C", StringComparison.OrdinalIgnoreCase) == true ? "206" : "216";

    public async Task<string> GetMttBatchXmlAsync(string companyCd, string userId, EInvoiceMttBatchRequest request)
    {
        ValidateCompany(companyCd);
        var ids = request.INVOICE_IDS.Distinct().ToArray();
        if (ids.Length == 0) throw new ArgumentException("Chọn hóa đơn MTT để xuất XML.");
        var batch = (await _db.QueryAsync<MttBatchRow>(Net_DB.Net_DB_Company,
            "CALL getEInvoiceMttBatchXml(@companyCd,@id)", new { companyCd, id = ids[0] })).SingleOrDefault();
        if (batch == null) return (await PrepareMttBatchAsync(companyCd, userId, request)).RAW_XML;
        var batchIds = JsonSerializer.Deserialize<List<MttItem>>(batch.ITEMS_JSON)!.Select(i => i.Id).ToHashSet();
        if (!batchIds.SetEquals(ids))
            throw new InvalidOperationException("Chọn đầy đủ hóa đơn của cùng một gói đã ký để xuất một XML có chữ ký hợp lệ.");
        return batch.SIGNED_XML!;
    }

    public async Task<EInvoiceDto> IssueMttAsync(string companyCd, string userId, long invoiceId)
    {
        ValidateCompany(companyCd);
        if (invoiceId <= 0) throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

        var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hóa đơn {invoiceId}.");
        if (!EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
            throw new InvalidOperationException("Chỉ phát hành được hóa đơn máy tính tiền.");
        if (invoice.IS_SIGNED == 1)
            throw new InvalidOperationException("Hóa đơn MTT đã ký gửi CQT.");
        if (IsMttIssued(invoice))
            throw new InvalidOperationException("Hóa đơn MTT đã phát hành.");
        if (invoice.BKE_INFO != null)
            throw new InvalidOperationException("Hóa đơn có bảng kê thay thế/điều chỉnh cần luồng thông điệp riêng.");

        await ApplySellerDefaultsAsync(companyCd, invoice);
        var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice)
            ?? throw new InvalidOperationException("Chưa có thông tin người bán.");
        var date = VietnamToday();
        var type = ResolveMttMessageType(invoice.KHHDON);
        var registration = (await _db.QueryAsync<MttRegistration>(Net_DB.Net_DB_Company,
            "CALL getEInvoiceMttRegistration(@companyCd,@mst)", new { companyCd, mst = seller.SELLER_TAX_CD })).SingleOrDefault();
        if (registration == null || (type == "206" ? registration.CMTMTTIEN : registration.KCMTMTTIEN) != 1)
            throw new InvalidOperationException("Chưa có tờ khai được CQT chấp nhận cho hình thức hóa đơn MTT này.");

        await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
        try
        {
            _ = await session.QueryFirstOrDefaultAsync<long>(
                "CALL lockEInvoiceMttSeller(@companyCd,@sellerId)",
                new { companyCd, sellerId = seller.SELLER_ID });

            var sequence = await _repository.GetSigningSequenceAsync(session, companyCd, invoice.INVOICE_ID, invoice.KHMSHDON, invoice.KHHDON);
            if (long.TryParse(seller.TO_SHDON, out var last) && last > 0
                && long.TryParse(sequence.NextShdon, CultureInfo.InvariantCulture, out var next)
                && next > last)
                throw new InvalidOperationException("Đã hết dải số hóa đơn.");

            invoice.NLAP = date;
            invoice.SHDON = sequence.NextShdon;
            invoice.MCCQT = type == "206" ? CreateMttCode(registration.MCCQT, invoice.KHMSHDON, date, invoiceId) : null;

            var root = XElement.Parse(await BuildRawXmlAsync(companyCd, invoice));
            root.Element("DSCKS")?.Remove();
            if (type == "216") root.Element("MCCQT")?.Remove();
            var xml = root.ToString(SaveOptions.DisableFormatting);
            var path = await _xmlStorageService.UploadInvoiceXmlAsync(companyCd, invoiceId, xml, date);

            await session.ExecuteAsync(
                "CALL issueEInvoiceMtt(@companyCd,@id,@version,@shdon,@nlap,@mccqt,@path,@userId)",
                new
                {
                    companyCd,
                    id = invoiceId,
                    version = invoice.DOC_VERSION,
                    shdon = invoice.SHDON,
                    nlap = date,
                    mccqt = invoice.MCCQT,
                    path,
                    userId
                });
            session.Commit();
        }
        catch
        {
            session.Rollback();
            throw;
        }

        return await GetByIdAsync(companyCd, invoiceId)
            ?? throw new InvalidOperationException("Failed to fetch issued MTT invoice");
    }

    public async Task<EInvoiceMttBatchPayload> PrepareMttBatchAsync(string companyCd, string userId, EInvoiceMttBatchRequest request)
    {
        ValidateCompany(companyCd);
        var ids = request.INVOICE_IDS.Distinct().ToArray();
        if (ids.Length == 0 || ids.Length > 1000 || ids.Any(id => id <= 0))
            throw new ArgumentException("Chọn từ 1 đến 1000 hóa đơn MTT để ký một gói XML.");
        var date = VietnamToday();
        var batchId = EInvoiceMessageXmlBuilder.GenerateMessageCode();
        var items = new List<MttItem>();
        var roots = new List<XElement>();
        EInvoiceSellerInfo? batchSeller = null;
        string? type = null;
        foreach (var id in ids)
        {
            var invoice = await GetEntityByIdAsync(companyCd, id) ?? throw new KeyNotFoundException($"Không tìm thấy hóa đơn {id}.");
            if (invoice.IS_SIGNED == 1 || !EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
                throw new InvalidOperationException("Gói MTT chỉ nhận hóa đơn máy tính tiền đã phát hành, chưa ký gửi.");
            if (!IsMttIssued(invoice))
                throw new InvalidOperationException("Hóa đơn MTT chưa phát hành. Phát hành trước khi ký gửi CQT.");
            if (!invoice.NLAP.HasValue || invoice.NLAP.Value.Date != date)
                throw new InvalidOperationException("Chỉ gửi trong ngày các hóa đơn MTT có ngày lập hôm nay.");
            if (invoice.BKE_INFO != null)
                throw new InvalidOperationException("Hóa đơn có bảng kê thay thế/điều chỉnh cần luồng thông điệp riêng.");
            await ApplySellerDefaultsAsync(companyCd, invoice);
            var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
            if (seller == null) throw new InvalidOperationException("Chưa có thông tin người bán.");
            var messageType = ResolveMttMessageType(invoice.KHHDON);
            if (messageType == "206" && !HasText(invoice.MCCQT))
                throw new InvalidOperationException("Hóa đơn MTT có mã thiếu MCCQT. Phát hành lại.");
            if (batchSeller == null)
            {
                batchSeller = seller;
                type = messageType;
            }
            else if (seller.SELLER_ID != batchSeller.SELLER_ID || invoice.KHHDON != items[0].Series || invoice.KHMSHDON != items[0].Form)
                throw new InvalidOperationException("Chọn các hóa đơn cùng người bán, mẫu số và ký hiệu trong một gói MTT.");
            else if (messageType != type)
                throw new InvalidOperationException("Gói MTT không trộn loại có mã và không mã.");

            var root = XElement.Parse(await BuildRawXmlAsync(companyCd, invoice));
            root.Element("DSCKS")?.Remove();
            if (type == "216") root.Element("MCCQT")?.Remove();
            roots.Add(root);
            items.Add(new MttItem(id, seller.SELLER_ID, invoice.DOC_VERSION, invoice.KHMSHDON!, invoice.KHHDON!, invoice.SHDON!, invoice.MCCQT));
        }
        var xml = MessageEnvelopeBuilder.Build(
            roots,
            type!,
            batchId,
            batchSeller!.SELLER_TAX_CD,
            version: "2.1.1",
            quantity: ids.Length,
            dataId: "DLieu-" + batchId,
            includeEmptyTaxpayerSignature: true,
            includeXmlDeclaration: false).RequestXml;
        if (Encoding.UTF8.GetByteCount(xml) > 2 * 1024 * 1024 - 16384)
            throw new InvalidOperationException("Gói XML vượt giới hạn 2 MB. Chọn ít hóa đơn hơn.");
        await _db.ExecuteAsync(Net_DB.Net_DB_Company,
            "CALL setEInvoiceMttBatch(@companyCd,@batchId,@userId,@date,@xml,@items)",
            new { companyCd, batchId, userId, date, xml, items = JsonSerializer.Serialize(items) });
        return new() { BATCH_ID = batchId, RAW_XML = xml, NLAP = date, COUNT = ids.Length };
    }

    internal static string CreateMttCode(string? issuedCode, string? form, DateTime date, long uniqueNumber)
    {
        var code = issuedCode?.Trim().ToUpperInvariant() ?? "";
        var match = Regex.Match(code, @"^M[1-9]-\d{2}-([A-Z0-9]{5})-\d{11}$");
        var prefix = match.Success ? match.Groups[1].Value : code;
        if (!Regex.IsMatch(prefix, "^[A-Z0-9]{5}$"))
            throw new InvalidOperationException("Chưa có MCCQT MTT hợp lệ từ tờ khai được chấp nhận (103).");
        if (!Regex.IsMatch(form ?? "", "^[1-9]$") || uniqueNumber <= 0 || uniqueNumber > 99999999999L)
            throw new InvalidOperationException("Không thể tạo MCCQT MTT: mẫu số hoặc số định danh vượt giới hạn.");
        return $"M{form}-{date:yy}-{prefix}-{uniqueNumber:00000000000}";
    }

    public async Task<int> SaveMttBatchAsync(string companyCd, string userId, string batchId, EInvoiceMttBatchSignRequest request)
    {
        ValidateCompany(companyCd);
        var batch = (await _db.QueryAsync<MttBatchRow>(Net_DB.Net_DB_Company,
            "CALL getEInvoiceMttBatch(@companyCd,@batchId,@userId)", new { companyCd, batchId, userId })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy gói MTT của người dùng.");
        var items = JsonSerializer.Deserialize<List<MttItem>>(batch.ITEMS_JSON)!;
        if (batch.SIGNED_XML == null)
        {
            if (batch.NLAP.Date != VietnamToday())
                throw new InvalidOperationException("Đã sang ngày mới. Tạo lại gói XML để ngày lập trùng ngày ký.");
            ValidateMttSignature(batch.RAW_XML, request.XML, batch.NLAP);
            var certificateSerial=TaxDocumentSignatureValidator.GetVerifiedCertificateSerialAtXPath(
                request.XML,"*[local-name()='CKSNNT']/*[local-name()='Signature']");
            var sellerInvoice=await GetEntityByIdAsync(companyCd,items[0].Id)
                ??throw new KeyNotFoundException($"Không tìm thấy hóa đơn {items[0].Id}.");
            var approvedCertificate=await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Company,
                "CALL isApprovedSigningCertificate(@company,'EINVOICE',@tax,@serial)",
                new{company=companyCd,tax=sellerInvoice.SELLER_TAX_CD??"",serial=certificateSerial});
            if(approvedCertificate!=1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");
            var roots = XDocument.Parse(request.XML, LoadOptions.PreserveWhitespace).Root!.Element("DLieu")!.Elements("HDon").ToArray();
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                using var locked = await session.QueryMultipleAsync("CALL lockEInvoiceMttBatch(@companyCd,@batchId,@userId,@sellerId)",
                    new { companyCd, batchId, userId, sellerId = items[0].SellerId });
                _ = (await locked.ReadAsync<long>()).Single();
                var current = (await locked.ReadAsync<MttBatchRow>()).Single();
                if (current.SIGNED_XML == null)
                {
                    foreach (var pair in items.Select((item, index) => (item, index)))
                    {
                        var item = pair.item;
                        var issued = await session.QueryFirstOrDefaultAsync<MttIssuedRow>(
                            @"SELECT SHDON, MCCQT, IFNULL(IS_SIGNED,0) AS IS_SIGNED
                              FROM einvoice_info
                              WHERE COMPANY_CD=@companyCd AND INVOICE_ID=@id AND ISDEL=0
                              FOR UPDATE",
                            new { companyCd, id = item.Id })
                            ?? throw new KeyNotFoundException($"Không tìm thấy hóa đơn {item.Id}.");
                        if (issued.IS_SIGNED == 1)
                            throw new InvalidOperationException("Hóa đơn MTT đã ký gửi. Tạo lại gói.");
                        if (!string.Equals(issued.SHDON?.Trim(), item.Number, StringComparison.Ordinal)
                            || !string.Equals(issued.MCCQT?.Trim() ?? "", item.Code?.Trim() ?? "", StringComparison.Ordinal))
                            throw new InvalidOperationException("Số hóa đơn hoặc MCCQT đã thay đổi. Phát hành/tạo lại gói.");

                        var path = await _xmlStorageService.UploadInvoiceXmlAsync(
                            companyCd, item.Id, roots[pair.index].ToString(SaveOptions.DisableFormatting), batch.NLAP);
                        await _repository.SetSignatureAsync(session, companyCd, userId, item.Id, path, batchId, 1, null);
                    }
                    if (batch.NLAP.Date != VietnamToday())
                        throw new InvalidOperationException("Đã sang ngày mới. Vui lòng ký lại.");
                    await session.ExecuteAsync("CALL setEInvoiceMttBatchSigned(@companyCd,@batchId,@xml)", new { companyCd, batchId, xml = request.XML });
                    batch.SIGNED_XML = request.XML;
                }
                else batch.SIGNED_XML = current.SIGNED_XML;
                session.Commit();
            }
            catch { session.Rollback(); throw; }
        }
        try { await _messageRepository.DispatchMttOutboxAsync(await _companyDatabaseResolver.ResolveDatabaseNameAsync(companyCd), companyCd, batchId); }
        catch (Exception ex) { _logger.LogWarning(ex, "MTT batch {BatchId} signed; awaiting outbox retry", batchId); }
        return items.Count;
    }

    internal static void ValidateMttSignature(string expected, string signed, DateTime date)
    {
        if (Encoding.UTF8.GetByteCount(signed) > 2 * 1024 * 1024) throw new ArgumentException("Gói XML đã ký vượt giới hạn 2 MB.");
        var xml = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        xml.LoadXml(signed);
        var root = XDocument.Parse(signed).Root ?? throw new ArgumentException("XML MTT không hợp lệ.");
        XNamespace ds = SignedXml.XmlDsigNamespaceUrl;
        var signature = root.Element("CKSNNT")?.Elements(ds + "Signature").SingleOrDefault()
            ?? throw new ArgumentException("Thiếu chữ ký gói MTT tại TDiep/CKSNNT.");
        var idValues = xml.SelectNodes("//*[@Id]")!.Cast<XmlElement>().Select(e => e.GetAttribute("Id")).ToArray();
        if (idValues.Distinct().Count() != idValues.Length) throw new ArgumentException("XML có Id trùng nhau.");
        var references = signature.Element(ds + "SignedInfo")!.Elements(ds + "Reference").Select(e => (string?)e.Attribute("URI")).ToArray();
        var dataId = (string?)root.Element("DLieu")?.Attribute("Id");
        XNamespace xades = "http://uri.etsi.org/01903/v1.3.2#";
        var properties = signature.Descendants(xades + "SignedProperties").SingleOrDefault();
        var time = properties?.Descendants(xades + "SigningTime").SingleOrDefault()?.Value;
        if (references.Length != 2 || !references.Contains("#" + dataId) || properties == null || !references.Contains("#" + (string?)properties.Attribute("Id"))
            || !DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedAt)
            || signedAt.ToOffset(TimeSpan.FromHours(7)).Date != date.Date)
            throw new ArgumentException("Chữ ký phải bao gồm DLieu, SignedProperties và ngày ký trùng ngày lập.");
        var verifier = new SignedXml(xml);
        verifier.LoadXml((XmlElement)xml.DocumentElement!.SelectSingleNode("*[local-name()='CKSNNT']/*[local-name()='Signature']")!);
        if (!verifier.CheckSignature()) throw new ArgumentException("Chữ ký số gói MTT không hợp lệ.");
        root.Element("CKSNNT")!.RemoveNodes();
        if (!XNode.DeepEquals(root, XDocument.Parse(expected).Root))
            throw new ArgumentException("Nội dung gói MTT đã thay đổi. Tạo lại gói và ký lại.");
    }
}
