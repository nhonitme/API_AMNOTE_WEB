using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceErrorNoticeService : IEInvoiceErrorNoticeService
    {
        private const string EInvoiceMailCode = "EINV";

        private readonly IEInvoiceErrorNoticeRepository _repository;
        private readonly IEInvoiceMessageRepository _messageRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly IEInvoiceErrorNoticePreviewService _previewService;
        private readonly IMailService _mailService;
        private readonly IMailSettingRepository _mailSettingRepository;
        private readonly DapperExecutor _db;
        private readonly ILogger<EInvoiceErrorNoticeService> _logger;

        public EInvoiceErrorNoticeService(
            IEInvoiceErrorNoticeRepository repository,
            IEInvoiceMessageRepository messageRepository,
            IEInvoiceTemplateRepository templateRepository,
            IEInvoiceErrorNoticePreviewService previewService,
            IMailService mailService,
            IMailSettingRepository mailSettingRepository,
            DapperExecutor db,
            ILogger<EInvoiceErrorNoticeService> logger)
        {
            _repository = repository;
            _messageRepository = messageRepository;
            _templateRepository = templateRepository;
            _previewService = previewService;
            _mailService = mailService;
            _mailSettingRepository = mailSettingRepository;
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EInvoiceErrorNoticeDto>> SearchAsync(string companyCd, EInvoiceErrorNoticeSearchRequest request)
        {
            ValidateCompany(companyCd);
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date)
                throw new ArgumentException("FromDate must be earlier than or equal to ToDate");

            var headers = (await _repository.GetHeadersAsync(
                companyCd,
                request.TbaoId,
                request.FromDate?.Date,
                request.ToDate?.Date,
                Common.NormalizeNullableText(request.Keyword),
                request.IsSigned)).ToList();

            if (request.IncludeDetails)
            {
                foreach (var header in headers)
                {
                    header.DETAILS = (await _repository.GetDetailsAsync(companyCd, header.TBAO_ID)).ToList();
                }
            }

            return headers.Select(EInvoiceErrorNoticeMapper.ToDto).ToList();
        }

        public async Task<EInvoiceErrorNoticeDto?> GetByIdAsync(string companyCd, long tbaoId)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            var notice = await GetEntityByIdAsync(companyCd, tbaoId);
            return notice == null ? null : EInvoiceErrorNoticeMapper.ToDto(notice);
        }

        public async Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetTransmissionMessagesAsync(string companyCd, long tbaoId)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            var notice = await GetEntityByIdAsync(companyCd, tbaoId)
                ?? throw new KeyNotFoundException("E-invoice error notice not found");
            var mtdiep = Common.NormalizeNullableText(notice.MTDIEP);
            if (!HasText(mtdiep))
            {
                return Array.Empty<EInvoiceMessageReceiveInfo>();
            }

            return await _messageRepository.GetReceiveMessagesByLookupCodeAsync(companyCd, mtdiep!);
        }

        public async Task<EInvoiceErrorNoticeDto> CreateAsync(string companyCd, string userId, EInvoiceErrorNoticeSaveRequest request)
        {
            ValidateCompany(companyCd);
            var notice = EInvoiceErrorNoticeMapper.ToEntity(request, companyCd);
            notice.TBAO_ID = 0;
            notice.IS_SIGNED = 0;
            notice.IS_MAIL = 0;
            NormalizeAndValidate(notice);
            ApplyMessageCodes(notice);
            var id = await PersistAsync(companyCd, userId, notice);
            _logger.LogInformation("EInvoice error notice created. Company: {CompanyCd}, TbaoId: {TbaoId}", companyCd, id);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created e-invoice error notice");
        }

        public async Task<EInvoiceErrorNoticeDto> UpdateAsync(string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeSaveRequest request)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            var existing = await GetEntityByIdAsync(companyCd, tbaoId)
                ?? throw new KeyNotFoundException("E-invoice error notice not found");
            EnsureEditable(existing);

            var notice = EInvoiceErrorNoticeMapper.ToEntity(request, companyCd);
            notice.TBAO_ID = tbaoId;
            notice.IS_SIGNED = existing.IS_SIGNED;
            NormalizeAndValidate(notice);
            ApplyMessageCodes(notice, existing);
            await PersistAsync(companyCd, userId, notice);
            _logger.LogInformation("EInvoice error notice updated. Company: {CompanyCd}, TbaoId: {TbaoId}", companyCd, tbaoId);
            return await GetByIdAsync(companyCd, tbaoId)
                ?? throw new InvalidOperationException("Failed to fetch updated e-invoice error notice");
        }

        public async Task<EInvoiceErrorNoticeSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long tbaoId)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            var notice = await GetEntityByIdAsync(companyCd, tbaoId)
                ?? throw new KeyNotFoundException("E-invoice error notice not found");

            if (notice.IS_SIGNED == 1)
            {
                return new EInvoiceErrorNoticeSigningPayloadDto
                {
                    TBAO_ID = notice.TBAO_ID,
                    RAW_XML = string.Empty,
                    IS_SIGNED = true
                };
            }

            EnsureEditable(notice);
            await EnsureMessageCodesAsync(companyCd, userId, notice);
            ApplyHeaderDefaults(notice);
            ApplySigningDates(notice);
            var rawXml = BuildRawXml(notice);
            return new EInvoiceErrorNoticeSigningPayloadDto
            {
                TBAO_ID = notice.TBAO_ID,
                RAW_XML = rawXml,
                IS_SIGNED = false
            };
        }

        public async Task<EInvoiceErrorNoticeDto> SaveSignatureAsync(string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeSignRequest request)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");
            if (request == null)
                throw new ArgumentException("Request body must be provided");

            var signedXml = Common.NormalizeNullableText(request.XML);
            if (!HasText(signedXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            var certificateSerial=TaxDocumentSignatureValidator.GetVerifiedCertificateSerial(signedXml!,"NNT");

            var notice = await GetEntityByIdAsync(companyCd, tbaoId)
                ?? throw new KeyNotFoundException("E-invoice error notice not found");
            if (notice.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice error notice is already signed");
            var approvedCertificate=await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Company,
                "CALL isApprovedSigningCertificate(@company,'EINVOICE',@tax,@serial)",
                new{company=companyCd,tax=notice.MST??"",serial=certificateSerial});
            if(approvedCertificate!=1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");

            EnsureEditable(notice);
            await EnsureMessageCodesAsync(companyCd, userId, notice);
            ApplySigningDates(notice);
            await PersistSigningDatesAsync(companyCd, userId, notice);

            var mtdiep = notice.MTDIEP
                ?? throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");
            var packagedMessage = EInvoiceMessageXmlBuilder.PackageSignedErrorNotice(signedXml!, notice, mtdiep);
            await SaveSignedXmlAsync(companyCd, userId, tbaoId, signedXml!, packagedMessage);
            _logger.LogInformation("EInvoice error notice signed. Company: {CompanyCd}, TbaoId: {TbaoId}, CertificateThumbprint: {CertificateThumbprint}", companyCd, tbaoId, request.CERTIFICATE_THUMBPRINT);

            return await GetByIdAsync(companyCd, tbaoId)
                ?? throw new InvalidOperationException("Failed to fetch signed e-invoice error notice");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, long tbaoId)
        {
            ValidateCompany(companyCd);
            if (tbaoId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            await EnsureDeletableAsync(companyCd, tbaoId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affected = await _repository.DeleteAsync(session, companyCd, tbaoId, userId);
                session.Commit();
                _logger.LogInformation("EInvoice error notice deleted. Company: {CompanyCd}, TbaoId: {TbaoId}", companyCd, tbaoId);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> tbaoIds)
        {
            ValidateCompany(companyCd);
            var ids = tbaoIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("TbaoIds");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affected = 0;
                foreach (var id in ids)
                {
                    await EnsureDeletableAsync(companyCd, id);
                    affected += await _repository.DeleteAsync(session, companyCd, id, userId);
                }

                session.Commit();
                _logger.LogInformation("EInvoice error notice batch deleted. Company: {CompanyCd}, Count: {Count}", companyCd, ids.Count);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<EInvoiceErrorNoticeSendMailResult> SendMailAsync(
            string companyCd,
            string userId,
            EInvoiceErrorNoticeSendMailRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateCompany(companyCd);
            if (request?.Items == null || request.Items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var items = request.Items
                .Where(x => x.TbaoId > 0)
                .GroupBy(x => x.TbaoId)
                .Select(group => group.Last())
                .ToList();

            if (items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var emailTemplate = await _templateRepository.GetActiveTemplateAsync(
                EInvoiceMailTemplateRenderer.EmailTemplateType,
                EInvoiceErrorNoticeMailTemplateRenderer.SendErrorNoticeTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice error notice email template '{EInvoiceErrorNoticeMailTemplateRenderer.SendErrorNoticeTemplateCd}' is not configured");

            var mailSetting = await _mailSettingRepository.GetActiveSettingAsync(companyCd, EInvoiceMailCode);
            var mailOptions = _mailService.ParseOptions(mailSetting?.CONFIG_JSON);
            var result = new EInvoiceErrorNoticeSendMailResult();

            foreach (var itemRequest in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = new EInvoiceErrorNoticeSendMailResultItem { TbaoId = itemRequest.TbaoId };
                try
                {
                    var buyerEmailList = EInvoiceBuyerEmailHelper.NormalizeBuyerEmailList(itemRequest.ToEmail);
                    if (string.IsNullOrWhiteSpace(buyerEmailList))
                    {
                        throw new ArgumentException("Recipient email is required");
                    }

                    EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(buyerEmailList);

                    var notice = await GetEntityByIdAsync(companyCd, itemRequest.TbaoId)
                        ?? throw new KeyNotFoundException("E-invoice error notice not found");

                    EnsureErrorNoticeReadyToSendMail(notice);

                    var signedXml = Common.NormalizeNullableText(notice.XML);
                    if (!HasText(signedXml))
                    {
                        throw new InvalidOperationException("Signed error notice XML is not available");
                    }

                    var displayNo = BuildErrorNoticeDisplayNo(notice);
                    var renderedMail = EInvoiceErrorNoticeMailTemplateRenderer.Render(
                        emailTemplate,
                        notice,
                        displayNo);
                    var baseFileName = SanitizeFileName($"TBao_{displayNo}");
                    var attachments = await BuildErrorNoticeMailAttachmentsAsync(
                        companyCd,
                        itemRequest.TbaoId,
                        signedXml!,
                        mailOptions,
                        baseFileName,
                        cancellationToken);

                    await _mailService.SendAsync(
                        companyCd,
                        EInvoiceMailCode,
                        new MailSendRequest
                        {
                            To = buyerEmailList,
                            Subject = renderedMail.Subject,
                            Body = renderedMail.Body,
                            IsBodyHtml = renderedMail.IsBodyHtml,
                            Attachments = attachments,
                        },
                        cancellationToken);

                    await using (var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company))
                    {
                        await _repository.SetMailSentAsync(session, companyCd, userId, itemRequest.TbaoId);
                        session.Commit();
                    }

                    item.Success = true;
                    item.ToEmail = buyerEmailList;
                    item.Message = "Sent";
                    result.Sent++;
                }
                catch (Exception ex)
                {
                    item.Success = false;
                    item.Message = ex.Message;
                    result.Skipped++;
                    _logger.LogWarning(
                        ex,
                        "EInvoice error notice send mail failed. Company: {CompanyCd}, TbaoId: {TbaoId}",
                        companyCd,
                        itemRequest.TbaoId);
                }

                result.Results.Add(item);
            }

            if (result.Sent == 0)
            {
                var firstError = result.Results.FirstOrDefault(x => !x.Success)?.Message;
                throw new InvalidOperationException(firstError ?? "Unable to send e-invoice error notice mail");
            }

            return result;
        }

        private static void EnsureErrorNoticeReadyToSendMail(EInvoiceErrorNoticeInfo notice)
        {
            if (notice.IS_SIGNED != 1)
            {
                throw new InvalidOperationException("E-invoice error notice is not signed");
            }
        }

        private static string BuildErrorNoticeDisplayNo(EInvoiceErrorNoticeInfo notice)
        {
            var so = Common.NormalizeNullableText(notice.SO);
            if (HasText(so))
            {
                return so!;
            }

            var mso = Common.NormalizeNullableText(notice.MSO);
            return HasText(mso) ? $"{mso}#{notice.TBAO_ID}" : $"#{notice.TBAO_ID}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "TBao.pdf" : sanitized;
        }

        private async Task<List<MailAttachmentDto>> BuildErrorNoticeMailAttachmentsAsync(
            string companyCd,
            long tbaoId,
            string signedXml,
            MailSendOptions mailOptions,
            string baseFileName,
            CancellationToken cancellationToken)
        {
            var attachments = new List<MailAttachmentDto>();

            if (mailOptions.AttachPdf)
            {
                var pdfBytes = await _previewService.ExportPdfAsync(companyCd, tbaoId, cancellationToken);
                attachments.Add(new MailAttachmentDto
                {
                    FileName = $"{baseFileName}.pdf",
                    Content = pdfBytes,
                    ContentType = "application/pdf",
                });
            }

            if (mailOptions.AttachXml)
            {
                attachments.Add(new MailAttachmentDto
                {
                    FileName = $"{baseFileName}.xml",
                    Content = Encoding.UTF8.GetBytes(signedXml),
                    ContentType = "application/xml",
                });
            }

            return attachments;
        }

        private async Task<EInvoiceErrorNoticeInfo?> GetEntityByIdAsync(string companyCd, long tbaoId)
        {
            var notice = (await _repository.GetHeadersAsync(companyCd, tbaoId, null, null, null, null)).FirstOrDefault();
            if (notice == null)
                return null;

            notice.DETAILS = (await _repository.GetDetailsAsync(companyCd, tbaoId)).ToList();
            return notice;
        }

        private async Task<long> PersistAsync(string companyCd, string userId, EInvoiceErrorNoticeInfo notice)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var tbaoId = await _repository.SetHeaderAsync(session, companyCd, userId, notice);
                await _repository.DeleteDetailsByNoticeAsync(session, companyCd, tbaoId, userId);

                var activeDetails = notice.DETAILS.Where(x => x.ISDEL != 1).ToList();
                for (var index = 0; index < activeDetails.Count; index++)
                {
                    var detail = activeDetails[index];
                    detail.DETAIL_ID = 0;
                    detail.TBAO_ID = tbaoId;
                    detail.COMPANY_CD = companyCd;
                    detail.STT ??= index + 1;
                    await _repository.SetDetailAsync(session, companyCd, userId, tbaoId, detail);
                }

                session.Commit();
                return tbaoId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task SaveSignedXmlAsync(string companyCd, string userId, long tbaoId, string signedXml, EInvoicePackagedMessage packagedMessage)
        {
            ValidateXml(signedXml, "XML");
            ValidateXml(packagedMessage.RequestXml, "PACKAGE_XML");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetSignatureAsync(session, companyCd, userId, tbaoId, signedXml, 1, null);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }

            await _messageRepository.EnqueueOutboundAsync(
                companyCd,
                userId,
                EInvoiceMessageTargetTypes.ErrorNotice,
                tbaoId,
                packagedMessage);
        }

        private const string DefaultMso = "04/SS-HĐĐT";
        private const string DefaultTen = "Thông báo hóa đơn điện tử có sai sót";

        private static void ApplyHeaderDefaults(EInvoiceErrorNoticeInfo notice)
        {
            notice.PBAN = Common.NormalizeNullableText(notice.PBAN) ?? "2.1.0";
            notice.MSO = DefaultMso;
            notice.TEN = DefaultTen;
        }

        private static void NormalizeAndValidate(EInvoiceErrorNoticeInfo notice)
        {
            ApplyHeaderDefaults(notice);
            notice.LOAI = notice.LOAI == 2 ? 2 : 1;
            notice.MCQT = Common.NormalizeNullableText(notice.MCQT) ?? string.Empty;
            notice.TCQT = Common.NormalizeNullableText(notice.TCQT) ?? string.Empty;
            notice.TNNT = Common.NormalizeNullableText(notice.TNNT) ?? string.Empty;
            notice.DDANH = Common.NormalizeNullableText(notice.DDANH) ?? string.Empty;
            notice.SO = Common.NormalizeNullableText(notice.SO);
            notice.MST = Common.NormalizeNullableText(notice.MST);
            notice.MGDDTU = Common.NormalizeNullableText(notice.MGDDTU);
            notice.IS_SIGNED = notice.IS_SIGNED == 1 ? 1 : 0;
            notice.IS_MAIL = notice.IS_MAIL == 1 ? 1 : 0;
            notice.ISDEL = 0;

            if (!HasText(notice.MCQT))
                throw EInvoiceValidationMessages.RequiredArgument("MCQT");
            if (!HasText(notice.TCQT))
                throw EInvoiceValidationMessages.RequiredArgument("TCQT");
            if (!HasText(notice.TNNT))
                throw EInvoiceValidationMessages.RequiredArgument("TNNT");
            if (!HasText(notice.DDANH))
                throw EInvoiceValidationMessages.RequiredArgument("DDANH");
            if (!notice.NTBAO.HasValue)
                notice.NTBAO = DateTime.Today;
            else
                notice.NTBAO = notice.NTBAO.Value.Date;
            if (notice.LOAI == 2 && !HasText(notice.SO))
                throw EInvoiceValidationMessages.RequiredArgument("SO");
            if (notice.LOAI == 2 && !notice.NTBCCQT.HasValue)
                throw EInvoiceValidationMessages.RequiredArgument("NTBCCQT");
            if (notice.IS_SIGNED == 1)
                throw new ArgumentException("Signed error notice cannot be saved");

            var activeDetails = notice.DETAILS.Where(x => x.ISDEL != 1).ToList();
            if (activeDetails.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("DETAIL_LINE");

            for (var index = 0; index < activeDetails.Count; index++)
            {
                var detail = activeDetails[index];
                detail.STT ??= index + 1;
                detail.MCCQT = Common.NormalizeNullableText(detail.MCCQT);
                detail.KHMSHDON = Common.NormalizeNullableText(detail.KHMSHDON);
                detail.KHHDON = Common.NormalizeNullableText(detail.KHHDON);
                detail.SHDON = Common.NormalizeNullableText(detail.SHDON);
                detail.LDO = Common.NormalizeNullableText(detail.LDO);
                detail.ISDEL = 0;

                if (!detail.NGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("NGAY", detail.STT ?? (index + 1));
                if (detail.LADHDDT <= 0)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("LADHDDT", detail.STT ?? (index + 1));
            }
        }

        private static string BuildRawXml(EInvoiceErrorNoticeInfo notice)
        {
            var rawXml = EInvoiceErrorNoticeXmlBuilder.Build(notice);
            if (!HasText(rawXml))
                throw new InvalidOperationException("Failed to generate e-invoice error notice XML");

            ValidateXml(rawXml, "XML");
            return rawXml;
        }

        private async Task EnsureMessageCodesAsync(string companyCd, string userId, EInvoiceErrorNoticeInfo notice, EInvoiceErrorNoticeInfo? existing = null)
        {
            var hadMtdiep = HasText(notice.MTDIEP) || HasText(existing?.MTDIEP);
            ApplyMessageCodes(notice, existing);

            if (hadMtdiep)
                return;

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetHeaderAsync(session, companyCd, userId, notice);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void ApplyMessageCodes(EInvoiceErrorNoticeInfo notice, EInvoiceErrorNoticeInfo? existing = null)
        {
            notice.MTDIEP = Common.NormalizeNullableText(notice.MTDIEP)
                ?? Common.NormalizeNullableText(existing?.MTDIEP);

            if (!HasText(notice.MTDIEP))
                notice.MTDIEP = EInvoiceMessageXmlBuilder.GenerateMessageCode();

            notice.MGDDTU = Common.NormalizeNullableText(notice.MGDDTU)
                ?? Common.NormalizeNullableText(existing?.MGDDTU);
        }

        private async Task PersistSigningDatesAsync(string companyCd, string userId, EInvoiceErrorNoticeInfo notice)
        {
            if (notice.TBAO_ID <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TBAO_ID");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetHeaderAsync(session, companyCd, userId, notice);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void ApplySigningDates(EInvoiceErrorNoticeInfo notice)
        {
            notice.NTBAO = DateTime.Today;
        }

        private static void EnsureEditable(EInvoiceErrorNoticeInfo notice)
        {
            if (notice.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice error notice is already signed");
        }

        private async Task EnsureDeletableAsync(string companyCd, long tbaoId)
        {
            var notice = (await _repository.GetHeadersAsync(companyCd, tbaoId, null, null, null, null)).FirstOrDefault()
                ?? throw new KeyNotFoundException("E-invoice error notice not found");

            if (notice.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice error notice is already signed");
        }

        private static bool HasText(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static void ValidateXml(string xml, string fieldName)
        {
            try
            {
                XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{fieldName} is invalid XML: {ex.Message}");
            }
        }

        private static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
                throw new UnauthorizedAccessException("Company code not found");
        }
    }
}
