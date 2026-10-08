using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceMinuteService : IEInvoiceMinuteService
    {
        private const string DocumentVersion = "2.1.0";

        private const string EInvoiceMailCode = "EINV";
        private const string PublicBuyerSignUserId = "PUBLIC_NMUA";

        private const string PublicLookupCompanyTargetsQuery = "CALL getEInvoicePublicLookupCompanyTargets(@p_TAX_CODE)";

        private readonly IEInvoiceMinuteRepository _repository;
        private readonly IEInvoiceSettingRepository _settingRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly IEInvoiceHtmlToPdfService _htmlToPdfService;
        private readonly IMailService _mailService;
        private readonly IMailSettingRepository _mailSettingRepository;
        private readonly DapperExecutor _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EInvoiceMinuteService> _logger;

        public EInvoiceMinuteService(
            IEInvoiceMinuteRepository repository,
            IEInvoiceSettingRepository settingRepository,
            IEInvoiceTemplateRepository templateRepository,
            IEInvoiceHtmlToPdfService htmlToPdfService,
            IMailService mailService,
            IMailSettingRepository mailSettingRepository,
            DapperExecutor db,
            IConfiguration configuration,
            ILogger<EInvoiceMinuteService> logger)
        {
            _repository = repository;
            _settingRepository = settingRepository;
            _templateRepository = templateRepository;
            _htmlToPdfService = htmlToPdfService;
            _mailService = mailService;
            _mailSettingRepository = mailSettingRepository;
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EInvoiceMinuteDto>> SearchAsync(string companyCd, EInvoiceMinuteSearchRequest request)
        {
            ValidateCompany(companyCd);
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date)
                throw new ArgumentException("FromDate must be earlier than or equal to ToDate");

            var headers = (await _repository.GetHeadersAsync(
                companyCd,
                request.BbanId,
                request.FromDate?.Date,
                request.ToDate?.Date,
                Common.NormalizeNullableText(request.Keyword),
                NormalizeStatusFilter(request.IsSigned))).ToList();

            if (request.IncludeReasons)
            {
                foreach (var header in headers)
                {
                    await LoadMinuteRowsAsync(companyCd, header);
                }
            }

            return headers.Select(EInvoiceMinuteMapper.ToDto).ToList();
        }

        public async Task<EInvoiceMinuteDto?> GetByIdAsync(string companyCd, long bbanId)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            var minute = await GetEntityByIdAsync(companyCd, bbanId);
            return minute == null ? null : EInvoiceMinuteMapper.ToDto(minute);
        }

        public async Task<EInvoiceMinuteDto> CreateAsync(string companyCd, string userId, EInvoiceMinuteSaveRequest request)
        {
            ValidateCompany(companyCd);
            var minute = EInvoiceMinuteMapper.ToEntity(request, companyCd);
            minute.BBAN_ID = 0;
            minute.IS_SIGNED = 0;
            minute.NMUA_IS_SIGNED = 0;
            minute.NMUA_SIGN_DT = null;
            minute.IS_MAIL = 0;
            ApplyLookupCode(minute);
            NormalizeAndValidate(minute);
            var id = await PersistAsync(companyCd, userId, minute);
            _logger.LogInformation("EInvoice minute created. Company: {CompanyCd}, BbanId: {BbanId}", companyCd, id);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created e-invoice minute");
        }

        public async Task<EInvoiceMinuteDto> UpdateAsync(string companyCd, string userId, long bbanId, EInvoiceMinuteSaveRequest request)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            var existing = await GetEntityByIdAsync(companyCd, bbanId)
                ?? throw new KeyNotFoundException("E-invoice minute not found");
            EnsureEditable(existing);

            var minute = EInvoiceMinuteMapper.ToEntity(request, companyCd);
            minute.BBAN_ID = bbanId;
            minute.IS_SIGNED = 0;
            minute.NMUA_IS_SIGNED = 0;
            minute.NMUA_SIGN_DT = null;
            ApplyLookupCode(minute, existing);
            NormalizeAndValidate(minute);
            await PersistAsync(companyCd, userId, minute);
            _logger.LogInformation("EInvoice minute updated. Company: {CompanyCd}, BbanId: {BbanId}", companyCd, bbanId);
            return await GetByIdAsync(companyCd, bbanId)
                ?? throw new InvalidOperationException("Failed to fetch updated e-invoice minute");
        }

        public async Task<EInvoiceMinuteSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long bbanId)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            var minute = await GetEntityByIdAsync(companyCd, bbanId)
                ?? throw new KeyNotFoundException("E-invoice minute not found");

            if (IsSigned(minute))
            {
                return new EInvoiceMinuteSigningPayloadDto
                {
                    BBAN_ID = minute.BBAN_ID,
                    RAW_XML = string.Empty,
                    IS_SIGNED = true
                };
            }

            NormalizeAndValidate(minute);
            var rawXml = await BuildRawXmlAsync(companyCd, minute);
            return new EInvoiceMinuteSigningPayloadDto
            {
                BBAN_ID = minute.BBAN_ID,
                RAW_XML = rawXml,
                IS_SIGNED = false
            };
        }

        public async Task<string> GetPreviewXmlAsync(string companyCd, long bbanId)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            var minute = await GetEntityByIdAsync(companyCd, bbanId)
                ?? throw new KeyNotFoundException("E-invoice minute not found");

            var signedXml = Common.NormalizeNullableText(minute.SIGNED_XML);
            if (IsSigned(minute) && HasText(signedXml))
                return signedXml!;

            return await BuildRawXmlAsync(companyCd, minute);
        }

        public async Task<EInvoiceMinutePublicLookupDto?> LookupPublicAsync(
            string taxCode,
            string lookupCode,
            CancellationToken cancellationToken = default)
        {
            var result = await FindPublicMinuteAsync(taxCode, lookupCode);
            return result == null ? null : await ToPublicLookupDtoAsync(result, cancellationToken);
        }

        public async Task<EInvoiceMinutePublicDownloadInfo?> GetPublicDownloadInfoAsync(string taxCode, string lookupCode)
        {
            var result = await FindPublicMinuteAsync(taxCode, lookupCode);
            if (result == null)
            {
                return null;
            }

            return new EInvoiceMinutePublicDownloadInfo
            {
                BBAN_ID = result.Minute.BBAN_ID,
                COMPANY_CD = result.CompanyCd,
                DB_NAME = result.DatabaseName,
                MTRACUU = result.Minute.MTRACUU,
                SBBAN = result.Minute.SBBAN,
            };
        }

        public async Task<string?> GetPublicXmlAsync(string taxCode, string lookupCode)
        {
            var result = await FindPublicMinuteAsync(taxCode, lookupCode);
            return result == null ? null : Common.NormalizeNullableText(result.Minute.SIGNED_XML);
        }

        public async Task<string?> GetPublicXslAsync(string taxCode, string lookupCode)
        {
            var downloadInfo = await GetPublicDownloadInfoAsync(taxCode, lookupCode);
            if (downloadInfo == null)
            {
                return null;
            }

            return await GetMinuteXslTemplateAsync();
        }

        public async Task<string?> GetPublicHtmlAsync(string taxCode, string lookupCode)
        {
            var result = await FindPublicMinuteAsync(taxCode, lookupCode);
            if (result == null)
            {
                return null;
            }

            var xml = Common.NormalizeNullableText(result.Minute.SIGNED_XML);
            if (!HasText(xml))
            {
                return null;
            }

            var xsl = await GetMinuteXslTemplateAsync();
            return EInvoiceXmlHtmlTransformService.Transform(xml!, xsl, lookupCompanyCd: result.CompanyCd);
        }

        public async Task<EInvoiceMinutePublicLookupDto?> SavePublicBuyerSignatureAsync(
            string taxCode,
            string lookupCode,
            EInvoiceMinuteSignRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentException("Request body must be provided");

            var signedXml = Common.NormalizeNullableText(request.XML);
            if (!HasText(signedXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");

            var result = await FindPublicMinuteAsync(taxCode, lookupCode);
            if (result == null)
            {
                return null;
            }

            if (!IsSigned(result.Minute))
                throw new InvalidOperationException("E-invoice minute is not signed by seller");
            if (result.Minute.NMUA_IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice minute is already signed by buyer");

            ValidateXml(signedXml!, "XML");
            ValidateSignedMinuteXml(signedXml!, requireBuyer: true);
            var checksum = ComputeSha256(signedXml!);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, result.DatabaseName);
            try
            {
                await _repository.SetBuyerSignatureAsync(
                    session,
                    result.CompanyCd,
                    PublicBuyerSignUserId,
                    result.Minute.BBAN_ID,
                    signedXml!,
                    checksum);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }

            _logger.LogInformation(
                "Public e-invoice minute buyer signed. Company: {CompanyCd}, BbanId: {BbanId}, CertificateThumbprint: {CertificateThumbprint}",
                result.CompanyCd,
                result.Minute.BBAN_ID,
                request.CERTIFICATE_THUMBPRINT);

            return await LookupPublicAsync(taxCode, lookupCode, cancellationToken);
        }

        public async Task<EInvoiceMinuteSendMailResult> SendMailAsync(
            string companyCd,
            string userId,
            EInvoiceMinuteSendMailRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateCompany(companyCd);
            if (request?.Items == null || request.Items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var items = request.Items
                .Where(x => x.BbanId > 0)
                .GroupBy(x => x.BbanId)
                .Select(group => group.Last())
                .ToList();

            if (items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var emailTemplate = await _templateRepository.GetActiveTemplateAsync(
                EInvoiceMailTemplateRenderer.EmailTemplateType,
                EInvoiceMinuteMailTemplateRenderer.SendMinuteTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice minute email template '{EInvoiceMinuteMailTemplateRenderer.SendMinuteTemplateCd}' is not configured");

            var mailSetting = await _mailSettingRepository.GetActiveSettingAsync(companyCd, EInvoiceMailCode);
            var mailOptions = _mailService.ParseOptions(mailSetting?.CONFIG_JSON);
            var result = new EInvoiceMinuteSendMailResult();

            foreach (var itemRequest in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = new EInvoiceMinuteSendMailResultItem { BbanId = itemRequest.BbanId };
                try
                {
                    var buyerEmailList = EInvoiceBuyerEmailHelper.NormalizeBuyerEmailList(itemRequest.ToEmail);
                    if (string.IsNullOrWhiteSpace(buyerEmailList))
                    {
                        throw new ArgumentException("Recipient email is required");
                    }

                    EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(buyerEmailList);

                    var minute = await GetEntityByIdAsync(companyCd, itemRequest.BbanId)
                        ?? throw new KeyNotFoundException("E-invoice minute not found");

                    EnsureMinuteReadyToSendMail(minute);

                    var signedXml = Common.NormalizeNullableText(minute.SIGNED_XML);
                    if (!HasText(signedXml))
                    {
                        throw new InvalidOperationException("Signed minute XML is not available");
                    }

                    var displayNo = BuildMinuteDisplayNo(minute);
                    var renderedMail = EInvoiceMinuteMailTemplateRenderer.Render(
                        emailTemplate,
                        minute,
                        displayNo,
                        _configuration["Frontend:Origin"]);
                    var baseFileName = SanitizeFileName($"BBan_{displayNo}");
                    var attachments = await BuildMinuteMailAttachmentsAsync(
                        companyCd,
                        itemRequest.BbanId,
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
                        await _repository.SetMailSentAsync(session, companyCd, userId, itemRequest.BbanId);
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
                        "EInvoice minute send mail failed. Company: {CompanyCd}, BbanId: {BbanId}",
                        companyCd,
                        itemRequest.BbanId);
                }

                result.Results.Add(item);
            }

            if (result.Sent == 0)
            {
                var firstError = result.Results.FirstOrDefault(x => !x.Success)?.Message;
                throw new InvalidOperationException(firstError ?? "Unable to send e-invoice minute mail");
            }

            return result;
        }

        private static void EnsureMinuteReadyToSendMail(EInvoiceMinuteInfo minute)
        {
            if (!IsSigned(minute))
            {
                throw new InvalidOperationException("E-invoice minute is not signed");
            }

            if (!HasText(Common.NormalizeNullableText(minute.SBBAN)))
            {
                throw new InvalidOperationException("Minute number (SBBAN) is not assigned");
            }
        }

        private static string BuildMinuteDisplayNo(EInvoiceMinuteInfo minute)
        {
            var displayNo = Common.NormalizeNullableText(minute.SBBAN);
            return HasText(displayNo) ? displayNo! : $"#{minute.BBAN_ID}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "BBan.pdf" : sanitized;
        }

        private async Task<List<MailAttachmentDto>> BuildMinuteMailAttachmentsAsync(
            string companyCd,
            long bbanId,
            string signedXml,
            MailSendOptions mailOptions,
            string baseFileName,
            CancellationToken cancellationToken)
        {
            var attachments = new List<MailAttachmentDto>();

            if (mailOptions.AttachPdf)
            {
                var pdfBytes = await ExportMinutePdfAsync(companyCd, bbanId, cancellationToken);
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

        private async Task<byte[]> ExportMinutePdfAsync(string companyCd, long bbanId, CancellationToken cancellationToken)
        {
            var xml = await GetPreviewXmlAsync(companyCd, bbanId);
            var template = await _templateRepository.GetActiveTemplateAsync(
                    EInvoiceXslPreviewHelper.TemplateTypeXsl,
                    EInvoiceMinutePreviewService.DefaultMinuteTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{EInvoiceMinutePreviewService.DefaultMinuteTemplateCd}' is not configured.");

            var xsl = Common.NormalizeNullableText(template.CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{EInvoiceMinutePreviewService.DefaultMinuteTemplateCd}' is empty.");
            }

            var html = EInvoiceXmlHtmlTransformService.Transform(xml, xsl, lookupCompanyCd: companyCd);
            return await _htmlToPdfService.ConvertAsync(html, cancellationToken);
        }

        public async Task<EInvoiceMinuteDto> SaveSignatureAsync(string companyCd, string userId, long bbanId, EInvoiceMinuteSignRequest request)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");
            if (request == null)
                throw new ArgumentException("Request body must be provided");

            var signedXml = Common.NormalizeNullableText(request.XML);
            if (!HasText(signedXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            var certificateSerial=TaxDocumentSignatureValidator.GetVerifiedCertificateSerial(signedXml!,"NBan");

            var minute = await GetEntityByIdAsync(companyCd, bbanId)
                ?? throw new KeyNotFoundException("E-invoice minute not found");
            if (IsSigned(minute))
                throw new InvalidOperationException("E-invoice minute is already signed");
            var approvedCertificate=await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Company,
                "CALL isApprovedSigningCertificate(@company,'EINVOICE',@tax,@serial)",
                new{company=companyCd,tax=minute.MSTNBAN,serial=certificateSerial});
            if(approvedCertificate!=1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");

            await SaveSignedXmlAsync(companyCd, userId, bbanId, signedXml!);
            _logger.LogInformation("EInvoice minute signed. Company: {CompanyCd}, BbanId: {BbanId}, CertificateThumbprint: {CertificateThumbprint}", companyCd, bbanId, request.CERTIFICATE_THUMBPRINT);

            return await GetByIdAsync(companyCd, bbanId)
                ?? throw new InvalidOperationException("Failed to fetch signed e-invoice minute");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, long bbanId)
        {
            ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            await EnsureDeletableAsync(companyCd, bbanId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affected = await _repository.DeleteAsync(session, companyCd, bbanId, userId);
                session.Commit();
                _logger.LogInformation("EInvoice minute deleted. Company: {CompanyCd}, BbanId: {BbanId}", companyCd, bbanId);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> bbanIds)
        {
            ValidateCompany(companyCd);
            var ids = bbanIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BbanIds");

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
                _logger.LogInformation("EInvoice minute batch deleted. Company: {CompanyCd}, Count: {Count}", companyCd, ids.Count);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task<EInvoiceMinuteInfo?> GetEntityByIdAsync(string companyCd, long bbanId, string? dbName = null)
        {
            var minute = (await _repository.GetHeadersAsync(companyCd, bbanId, null, null, null, null, dbName)).FirstOrDefault();
            if (minute == null)
                return null;

            await LoadMinuteRowsAsync(companyCd, minute, dbName);
            return minute;
        }

        private async Task<long> PersistAsync(string companyCd, string userId, EInvoiceMinuteInfo minute)
        {
            var builder = await CreateXmlBuilderAsync(companyCd, minute.SELLER_ID, minute.DVTTE);
            minute.NDBBAN_XML = builder.BuildContentXml(minute);
            minute.CHECKSUM = ComputeSha256(minute.NDBBAN_XML);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var bbanId = await _repository.SetHeaderAsync(session, companyCd, userId, minute);
                await _repository.DeleteReasonsByMinuteAsync(session, companyCd, bbanId);

                var rowsToPersist = EInvoiceMinuteMapper.BuildPersistRows(minute);
                foreach (var row in rowsToPersist)
                {
                    row.REASON_ID = 0;
                    row.BBAN_ID = bbanId;
                    await _repository.SetReasonAsync(session, companyCd, userId, bbanId, row);
                }

                session.Commit();
                return bbanId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task SaveSignedXmlAsync(string companyCd, string userId, long bbanId, string signedXml)
        {
            ValidateXml(signedXml, "XML");
            ValidateSignedMinuteXml(signedXml, requireBuyer: false);
            var checksum = ComputeSha256(signedXml);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetSignatureAsync(session, companyCd, userId, bbanId, signedXml, checksum);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task LoadMinuteRowsAsync(string companyCd, EInvoiceMinuteInfo minute, string? dbName = null)
        {
            var rows = (await _repository.GetReasonsAsync(companyCd, minute.BBAN_ID, dbName)).ToList();
            EInvoiceMinuteMapper.SplitStoredRows(rows, minute);
        }

        private async Task<PublicMinuteLookupResult?> FindPublicMinuteAsync(string taxCode, string lookupCode)
        {
            var normalizedLookupCode = NormalizeLookupCode(lookupCode);
            if (!HasText(normalizedLookupCode) || !IsSafeLookupCode(normalizedLookupCode!))
            {
                throw new ArgumentException("MTRACUU is invalid");
            }

            var normalizedTaxCode = NormalizeTaxCode(taxCode);
            if (!HasText(normalizedTaxCode))
            {
                throw EInvoiceValidationMessages.RequiredArgument("TAX_CD");
            }

            var targets = await GetPublicLookupCompanyTargetsAsync(normalizedTaxCode!);
            foreach (var target in targets)
            {
                try
                {
                    var header = (await _repository.GetHeadersAsync(
                            target.COMPANY_CD,
                            null,
                            null,
                            null,
                            normalizedLookupCode,
                            null,
                            target.DB_NAME))
                        .FirstOrDefault(x =>
                            string.Equals(NormalizeLookupCode(x.MTRACUU), normalizedLookupCode, StringComparison.OrdinalIgnoreCase) &&
                            TaxCodeMatchesMinute(normalizedTaxCode!, x));

                    if (header == null)
                    {
                        continue;
                    }

                    var minute = await GetEntityByIdAsync(target.COMPANY_CD, header.BBAN_ID, target.DB_NAME);
                    if (minute == null || !IsSigned(minute) || !HasText(Common.NormalizeNullableText(minute.SIGNED_XML)))
                    {
                        continue;
                    }

                    return new PublicMinuteLookupResult(target.COMPANY_CD, target.DB_NAME, minute);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Public e-invoice minute lookup skipped company after database query failed. Company: {CompanyCd}, DbName: {DbName}",
                        target.COMPANY_CD,
                        target.DB_NAME);
                }
            }

            return null;
        }

        private async Task<IReadOnlyList<PublicLookupCompanyTarget>> GetPublicLookupCompanyTargetsAsync(string normalizedTaxCode)
        {
            var targets = (await _db.QueryAsync<PublicLookupCompanyTarget>(
                    Net_DB.Net_DB_Manager,
                    PublicLookupCompanyTargetsQuery,
                    new { p_TAX_CODE = normalizedTaxCode }))
                .Where(x => HasText(x.COMPANY_CD) && HasText(x.DB_NAME))
                .ToList();

            return targets;
        }

        private static bool TaxCodeMatchesMinute(string normalizedTaxCode, EInvoiceMinuteInfo minute)
        {
            return string.Equals(NormalizeTaxCode(minute.MSTNBAN), normalizedTaxCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeTaxCode(minute.MSTNMUA), normalizedTaxCode, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<EInvoiceMinutePublicLookupDto> ToPublicLookupDtoAsync(
            PublicMinuteLookupResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var minute = result.Minute;
            var xml = Common.NormalizeNullableText(minute.SIGNED_XML)
                ?? throw new InvalidOperationException("Signed minute XML is not available");
            var xsl = await GetMinuteXslTemplateAsync();
            var html = EInvoiceXmlHtmlTransformService.Transform(xml, xsl, lookupCompanyCd: result.CompanyCd);
            var detail = EInvoiceMinuteMapper.ToDto(minute);

            return new EInvoiceMinutePublicLookupDto
            {
                BBAN_ID = minute.BBAN_ID,
                MTRACUU = minute.MTRACUU,
                TBBAN = minute.TBBAN,
                SBBAN = minute.SBBAN,
                NBBAN = minute.NBBAN,
                TCHDON = minute.TCHDON,
                NBAN = minute.NBAN,
                MSTNBAN = minute.MSTNBAN,
                DCNBAN = minute.DCNBAN,
                NMUA = minute.NMUA,
                MSTNMUA = minute.MSTNMUA,
                DCNMUA = minute.DCNMUA,
                KHMSHDON = minute.KHMSHDON,
                KHHDON = minute.KHHDON,
                SHDON = minute.SHDON,
                NLAP = minute.NLAP,
                IS_SIGNED = minute.IS_SIGNED,
                NMUA_IS_SIGNED = minute.NMUA_IS_SIGNED,
                NMUA_SIGN_DT = minute.NMUA_SIGN_DT,
                CAN_SIGN = IsSigned(minute) && minute.NMUA_IS_SIGNED != 1,
                CAN_DOWNLOAD_PDF = true,
                CAN_DOWNLOAD_XML = true,
                CAN_DOWNLOAD_XSL = true,
                CAN_DOWNLOAD_HTML = true,
                XML = xml,
                XSL = xsl,
                HTML = html,
                REASONS = detail.REASONS,
                LINES_BEFORE = detail.LINES_BEFORE,
                LINES_AFTER = detail.LINES_AFTER,
                TOTAL_BEFORE = detail.TOTAL_BEFORE,
                TOTAL_AFTER = detail.TOTAL_AFTER,
            };
        }

        private async Task<string> GetMinuteXslTemplateAsync()
        {
            var template = await _templateRepository.GetActiveTemplateAsync(
                    EInvoiceXslPreviewHelper.TemplateTypeXsl,
                    EInvoiceMinutePreviewService.DefaultMinuteTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{EInvoiceMinutePreviewService.DefaultMinuteTemplateCd}' is not configured.");

            return Common.NormalizeNullableText(template.CONTENT)
                ?? throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{EInvoiceMinutePreviewService.DefaultMinuteTemplateCd}' is empty.");
        }

        private static void ApplyLookupCode(EInvoiceMinuteInfo minute, EInvoiceMinuteInfo? existing = null)
        {
            minute.MTRACUU = Common.NormalizeNullableText(minute.MTRACUU)
                ?? Common.NormalizeNullableText(existing?.MTRACUU);

            minute.MTRACUU = HasText(minute.MTRACUU)
                ? minute.MTRACUU!.ToUpperInvariant()
                : GenerateLookupCode();
        }

        private static string GenerateLookupCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            const int lookupCodeBodyLength = 18;
            Span<byte> bytes = stackalloc byte[lookupCodeBodyLength];
            RandomNumberGenerator.Fill(bytes);

            var chars = new char[lookupCodeBodyLength];
            for (var i = 0; i < lookupCodeBodyLength; i++)
            {
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            }

            return $"BB{new string(chars)}";
        }

        private static void NormalizeAndValidate(EInvoiceMinuteInfo minute)
        {
            minute.PBAN = DocumentVersion;
            minute.TBBAN = Common.NormalizeNullableText(minute.TBBAN)
                ?? (minute.TCHDON == 1 ? "Biên bản thay thế hóa đơn" : "Biên bản điều chỉnh hóa đơn");
            minute.SBBAN = Common.NormalizeNullableText(minute.SBBAN) ?? string.Empty;
            minute.TCHDON = minute.TCHDON == 1 ? 1 : 2;
            minute.NBAN = Common.NormalizeNullableText(minute.NBAN) ?? string.Empty;
            minute.MSTNBAN = Common.NormalizeNullableText(minute.MSTNBAN) ?? string.Empty;
            minute.DCNBAN = Common.NormalizeNullableText(minute.DCNBAN);
            minute.NMUA = Common.NormalizeNullableText(minute.NMUA) ?? string.Empty;
            minute.MSTNMUA = Common.NormalizeNullableText(minute.MSTNMUA);
            minute.DCNMUA = Common.NormalizeNullableText(minute.DCNMUA);
            minute.KHMSHDON = Common.NormalizeNullableText(minute.KHMSHDON) ?? string.Empty;
            minute.KHHDON = Common.NormalizeNullableText(minute.KHHDON);
            minute.SHDON = Common.NormalizeNullableText(minute.SHDON);
            minute.DVTTE = (Common.NormalizeNullableText(minute.DVTTE) ?? "VND").ToUpperInvariant();
            var isForeignCurrency = !string.Equals(minute.DVTTE, "VND", StringComparison.OrdinalIgnoreCase);
            if (!isForeignCurrency)
            {
                minute.TGIA = 1m;
            }
            else if (minute.TGIA is null or <= 0m)
            {
                throw EInvoiceValidationMessages.RequiredArgument("TGIA");
            }

            if (minute.DVTTE.Length != 3)
                throw new ArgumentException("DVTTE must contain 3 characters");

            if (minute.TGIA <= 0)
                throw new ArgumentException("TGIA must be greater than zero");

            minute.MTRACUU = Common.NormalizeNullableText(minute.MTRACUU)?.ToUpperInvariant();
            minute.TTKHAC_XML = Common.NormalizeNullableText(minute.TTKHAC_XML);
            minute.IS_SIGNED = minute.IS_SIGNED == 1 ? 1 : 0;
            minute.NMUA_IS_SIGNED = minute.NMUA_IS_SIGNED == 1 ? 1 : 0;
            if (minute.NMUA_IS_SIGNED != 1)
                minute.NMUA_SIGN_DT = null;
            minute.IS_MAIL = minute.IS_MAIL == 1 ? 1 : 0;
            minute.ISDEL = 0;

            if (!HasText(minute.SBBAN))
                throw EInvoiceValidationMessages.RequiredArgument("SBBAN");
            if (!minute.NBBAN.HasValue)
                throw EInvoiceValidationMessages.RequiredArgument("NBBAN");
            if (!HasText(minute.NBAN))
                throw EInvoiceValidationMessages.RequiredArgument("NBAN");
            if (!HasText(minute.MSTNBAN))
                throw EInvoiceValidationMessages.RequiredArgument("MSTNBAN");
            if (!HasText(minute.NMUA))
                throw EInvoiceValidationMessages.RequiredArgument("NMUA");
            if (!HasText(minute.KHMSHDON))
                throw EInvoiceValidationMessages.RequiredArgument("KHMSHDON");
            if (IsSigned(minute))
                throw new ArgumentException("Signed e-invoice minute cannot be saved");

            var activeReasons = minute.REASONS.Where(x => x.ISDEL != 1).ToList();
            if (activeReasons.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_REASONS");

            for (var index = 0; index < activeReasons.Count; index++)
            {
                var reason = activeReasons[index];
                reason.SORT_ORDER = index + 1;
                reason.LDO = Common.NormalizeNullableText(reason.LDO) ?? string.Empty;
                reason.ISDEL = 0;

                if (!HasText(reason.LDO))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("LDO", reason.SORT_ORDER);
            }

            NormalizeLines(minute.LINES_BEFORE, EInvoiceMinuteMapper.LineSideBefore);
            NormalizeLines(minute.LINES_AFTER, EInvoiceMinuteMapper.LineSideAfter);
            minute.TOTAL_BEFORE = NormalizeTotalLine(minute.TOTAL_BEFORE, EInvoiceMinuteMapper.LineSideBeforeTotal);
            minute.TOTAL_AFTER = NormalizeTotalLine(minute.TOTAL_AFTER, EInvoiceMinuteMapper.LineSideAfterTotal);

            if (HasText(minute.TTKHAC_XML))
                EInvoiceMinuteXmlBuilder.ValidateAdditionalInfoXml(minute.TTKHAC_XML);
        }

        private async Task<string> BuildRawXmlAsync(string companyCd, EInvoiceMinuteInfo minute)
        {
            var builder = await CreateXmlBuilderAsync(companyCd, minute.SELLER_ID, minute.DVTTE);
            var rawXml = builder.Build(minute);
            if (!HasText(rawXml))
                throw new InvalidOperationException("Failed to generate e-invoice minute XML");

            ValidateXml(rawXml, "XML");
            return rawXml;
        }

        private async Task<EInvoiceMinuteXmlBuilder> CreateXmlBuilderAsync(string companyCd, long? sellerId, string? currencyCode)
        {
            var decimalSettings = await _settingRepository.GetDecimalSettingsAsync(companyCd, null, null, null, null, false);
            var formatter = EInvoiceDecimalFormatter.Create(decimalSettings);
            return new EInvoiceMinuteXmlBuilder(formatter, currencyCode);
        }

        private static void EnsureEditable(EInvoiceMinuteInfo minute)
        {
            if (IsSigned(minute))
                throw new InvalidOperationException("E-invoice minute is already signed");
        }

        private async Task EnsureDeletableAsync(string companyCd, long bbanId)
        {
            var minute = (await _repository.GetHeadersAsync(companyCd, bbanId, null, null, null, null)).FirstOrDefault()
                ?? throw new KeyNotFoundException("E-invoice minute not found");

            if (IsSigned(minute))
                throw new InvalidOperationException("E-invoice minute is already signed");
        }

        private static bool IsSigned(EInvoiceMinuteInfo minute)
        {
            return minute.IS_SIGNED == 1;
        }

        private static int? NormalizeStatusFilter(int? isSigned)
        {
            if (!isSigned.HasValue || isSigned.Value < 0)
                return null;

            return isSigned.Value == 1 ? 1 : 0;
        }

        private static void NormalizeLines(List<EInvoiceMinuteLine> lines, int lineSide)
        {
            var activeLines = lines.Where(x => x.ISDEL != 1).ToList();
            for (var index = 0; index < activeLines.Count; index++)
            {
                var line = activeLines[index];
                line.LINE_SIDE = lineSide;
                line.SORT_ORDER = index + 1;
                line.ISDEL = 0;
                line.MHHDVU = Common.NormalizeNullableText(line.MHHDVU);
                line.THHDVU = Common.NormalizeNullableText(line.THHDVU);
                line.DVTINH = Common.NormalizeNullableText(line.DVTINH);
                line.TSUAT = Common.NormalizeNullableText(line.TSUAT);
                line.EXTRA_JSON = Common.NormalizeNullableText(line.EXTRA_JSON);
            }
        }

        private static EInvoiceMinuteLine NormalizeTotalLine(EInvoiceMinuteLine? totalLine, int lineSide)
        {
            totalLine ??= new EInvoiceMinuteLine();
            totalLine.LINE_SIDE = lineSide;
            totalLine.SORT_ORDER = 9999;
            totalLine.ISDEL = 0;
            totalLine.LDO = string.Empty;
            totalLine.THHDVU = Common.NormalizeNullableText(totalLine.THHDVU) ?? "Tổng cộng";
            totalLine.THTIEN ??= 0m;
            totalLine.TTHUE ??= 0m;
            totalLine.TSAUTHUE ??= 0m;
            return totalLine;
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

        private static void ValidateSignedMinuteXml(string xml, bool requireBuyer)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null || !IsElement(root, "BBan"))
                throw new ArgumentException("XML must be a BBan document");

            var content = root.Elements().FirstOrDefault(element => IsElement(element, "NDBBan"));
            if (!HasText(content?.Attribute("Id")?.Value))
                throw new ArgumentException("NDBBan Id attribute is required before saving signed BBan XML");

            var signatureList = root.Elements().FirstOrDefault(element => IsElement(element, "DSCKS"));
            if (!HasDigitalSignature(signatureList, "NBan"))
                throw new ArgumentException("Signed BBan XML must contain DSCKS/NBan/Signature");
            if (requireBuyer && !HasDigitalSignature(signatureList, "NMua"))
                throw new ArgumentException("Buyer-signed BBan XML must contain DSCKS/NMua/Signature");
        }

        private static bool HasDigitalSignature(XElement? signatureList, string containerName)
        {
            var container = signatureList?
                .Elements()
                .FirstOrDefault(element => IsElement(element, containerName));
            var signature = container?
                .Elements()
                .FirstOrDefault(element => IsElement(element, "Signature"));

            return signature != null &&
                signature.Elements().Any(element => IsElement(element, "SignedInfo")) &&
                signature.Elements().Any(element => IsElement(element, "SignatureValue"));
        }

        private static bool IsElement(XElement element, string localName)
        {
            return string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeLookupCode(string? value)
        {
            return Common.NormalizeNullableText(value)?.ToUpperInvariant();
        }

        private static string? NormalizeTaxCode(string? value)
        {
            var text = Common.NormalizeNullableText(value);
            if (!HasText(text))
            {
                return null;
            }

            return text!
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .ToUpperInvariant();
        }

        private static bool IsSafeLookupCode(string value)
        {
            return value.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_');
        }

        private static string ComputeSha256(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
                throw new UnauthorizedAccessException("Company code not found");
        }

        private sealed record PublicMinuteLookupResult(string CompanyCd, string DatabaseName, EInvoiceMinuteInfo Minute);

        private sealed class PublicLookupCompanyTarget
        {
            public string COMPANY_CD { get; set; } = string.Empty;
            public string DB_NAME { get; set; } = string.Empty;
        }
    }
}
