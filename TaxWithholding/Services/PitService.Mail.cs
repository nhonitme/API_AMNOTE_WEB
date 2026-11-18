using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed partial class PitService
{
    // Reuse the SMTP configuration managed on the invoice mail settings screen.
    private const string PitSmtpMailCode = "EINV";

    public async Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> MailHistory(string company, string kind, long id)
    {
        var refType = PitKinds.Target(kind);
        // Authorize access to the company document before querying the shared manager DB.
        _ = await repository.Get(company, kind, id);
        return await mailHistory.GetHistoryAsync(company, refType, id);
    }

    public async Task<PitSendMailResult> SendMail(
        string company, string user, string kind, long id, PitSendMailRequest request,
        CancellationToken cancellationToken = default)
    {
        var refType = PitKinds.Target(kind);
        var row = await repository.Get(company, kind, id);
        if (row.IS_SIGNED != 1 || string.IsNullOrWhiteSpace(row.SIGNED_XML))
            throw new InvalidOperationException("Chỉ gửi mail chứng từ đã ký và có XML đã ký.");

        var to = EInvoiceBuyerEmailHelper.NormalizeBuyerEmailList(request.ToEmail, 1000);
        if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("Vui lòng nhập email người nhận.");
        EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(to, "Email người nhận");

        var databaseName = await databases.ResolveDatabaseNameAsync(company);
        var history = new EInvoiceEmailHistoryCreateRequest
        {
            COMPANY_CD = company, DB_NAME = databaseName, REF_TYPE = refType, REF_ID = id,
            TO_EMAIL = to, CREATE_BY = user, SEND_TYPE = "EMAIL"
        };

        // A failure after SMTP succeeds must never be reported as a failed send (or retried).
        try
        {
            var setting = await mailSettings.GetActiveSettingAsync(company, PitSmtpMailCode)
                ?? throw new InvalidOperationException("Chưa cấu hình SMTP. Vui lòng kiểm tra cấu hình gửi mail hóa đơn.");
            var options = mailService.ParseOptions(setting.CONFIG_JSON);
            history.FROM_EMAIL = setting.FROM_EMAIL;
            history.CC_EMAIL = options.Cc ?? "";
            history.BCC_EMAIL = options.Bcc ?? "";

            var title = kind switch
            {
                PitKinds.Certificate => "Chứng từ khấu trừ thuế thu nhập cá nhân",
                PitKinds.Declaration => "Tờ khai đăng ký sử dụng chứng từ điện tử",
                PitKinds.ErrorNotice => "Thông báo chứng từ điện tử đã lập sai",
                _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
            };
            var displayNo = row.DOC_NO is > 0
                ? $"{row.SERIES} / {row.DOC_NO.Value.ToString("D8", CultureInfo.InvariantCulture)}"
                : $"#{id}";
            var date = row.DOC_DATE?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
            history.MAIL_SUBJECT = $"{title} - {displayNo}";
            var lookupBlock = "";
            if (kind == PitKinds.Certificate)
            {
                var lookupCode = (row.MTRACUU ?? "").Trim().ToUpperInvariant();
                var payerTax = row is PitCertificateDto certificate
                    ? (certificate.TCTTNHAP_MST ?? "").Trim()
                    : (row.TAX_CD ?? "").Trim();
                var origin = (configuration["Frontend:Origin"] ?? "").Trim().TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(lookupCode) && !string.IsNullOrWhiteSpace(payerTax) && !string.IsNullOrWhiteSpace(origin))
                {
                    var lookupUrl = $"{origin}/tra-cuu-chung-tu-tncn-amnote/{Uri.EscapeDataString(payerTax)}?mtracuu={Uri.EscapeDataString(lookupCode)}";
                    lookupBlock = $"<p>Mã tra cứu: <strong>{WebUtility.HtmlEncode(lookupCode)}</strong><br>"
                        + $"Tra cứu tại: <a href=\"{WebUtility.HtmlEncode(lookupUrl)}\">{WebUtility.HtmlEncode(lookupUrl)}</a></p>";
                }
                else if (!string.IsNullOrWhiteSpace(lookupCode))
                {
                    lookupBlock = $"<p>Mã tra cứu: <strong>{WebUtility.HtmlEncode(lookupCode)}</strong></p>";
                }
            }

            history.MAIL_BODY = $"<p>Kính gửi Quý khách,</p><p>Xin gửi {WebUtility.HtmlEncode(title.ToLowerInvariant())} đã ký.</p>"
                + $"<p>Số tham chiếu: {WebUtility.HtmlEncode(displayNo)}<br>Ngày lập: {date}<br>"
                + $"Tên: {WebUtility.HtmlEncode(row.DISPLAY_NAME)}<br>Mã số thuế: {WebUtility.HtmlEncode(row.TAX_CD)}</p>"
                + lookupBlock
                + "<p>Vui lòng xem tài liệu đính kèm.</p><p>Trân trọng.</p>";

            var attachments = new List<MailAttachmentDto>();
            var fileName = $"PIT_{kind}_{id}";
            if (options.AttachXml)
                attachments.Add(new() { FileName = fileName + ".xml", ContentType = "application/xml", Content = Encoding.UTF8.GetBytes(row.SIGNED_XML) });
            if (options.AttachPdf)
                attachments.Add(new() { FileName = fileName + ".pdf", ContentType = "application/pdf", Content = await PrintPdf(company, kind, id, cancellationToken) });
            if (attachments.Count == 0)
                throw new InvalidOperationException("Vui lòng bật đính kèm XML hoặc PDF trong cấu hình gửi mail.");
            history.ATTACHMENT_INFO = JsonSerializer.Serialize(attachments.Select(x => new { x.FileName, x.ContentType, Size = x.Content.Length }));
            cancellationToken.ThrowIfCancellationRequested();
            await mailService.SendWithSettingAsync(setting, new MailSendRequest
            {
                To = to, Subject = history.MAIL_SUBJECT, Body = history.MAIL_BODY,
                IsBodyHtml = true, Attachments = attachments
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            history.SEND_STATUS = "ERROR";
            history.ERROR_MESSAGE = ex.Message;
            await SaveMailHistory(history);
            throw;
        }

        history.SEND_STATUS = "SENT";
        history.SEND_DT = DateTime.UtcNow.AddHours(7);
        var saved = await SaveMailHistory(history);
        return new(true, saved, saved ? null : "Mail đã gửi nhưng chưa lưu được lịch sử. Không gửi lại chỉ vì lỗi lưu lịch sử.");
    }

    private async Task<bool> SaveMailHistory(EInvoiceEmailHistoryCreateRequest history)
    {
        try
        {
            await mailHistory.InsertAsync(history);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot save PIT mail history. Company={Company}, Type={Type}, Id={Id}, Status={Status}",
                history.COMPANY_CD, history.REF_TYPE, history.REF_ID, history.SEND_STATUS);
            return false;
        }
    }
}
