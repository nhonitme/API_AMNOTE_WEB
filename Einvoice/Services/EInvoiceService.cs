using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    public partial class EInvoiceService : IEInvoiceService
    {
        private const string PublicLookupCompanyTargetsQuery = "CALL getEInvoicePublicLookupCompanyTargets(@p_TAX_CODE)";

        private readonly IEInvoiceRepository _repository;
        private readonly IEInvoiceMessageRepository _messageRepository;
        private readonly IEInvoiceEmailHistoryRepository _emailHistoryRepository;
        private readonly IEInvoiceSettingRepository _settingRepository;
        private readonly IEInvoiceSellerRepository _sellerRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly ISysWorkingCalendarService _workingCalendarService;
        private readonly IEInvoicePrintService _printService;
        private readonly IMailService _mailService;
        private readonly IMailSettingRepository _mailSettingRepository;
        private readonly IEInvoiceXmlStorageService _xmlStorageService;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private readonly DapperExecutor _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EInvoiceService> _logger;

        private const string EInvoiceMailCode = "EINV";
        private const int EInvoiceMailStatusSent = 1;
        private const int EInvoiceMailStatusError = 2;

        public EInvoiceService(
            IEInvoiceRepository repository,
            IEInvoiceMessageRepository messageRepository,
            IEInvoiceEmailHistoryRepository emailHistoryRepository,
            IEInvoiceSettingRepository settingRepository,
            IEInvoiceSellerRepository sellerRepository,
            IEInvoiceTemplateRepository templateRepository,
            ISysWorkingCalendarService workingCalendarService,
            IEInvoicePrintService printService,
            IMailService mailService,
            IMailSettingRepository mailSettingRepository,
            IEInvoiceXmlStorageService xmlStorageService,
            ICompanyDatabaseResolver companyDatabaseResolver,
            DapperExecutor db,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<EInvoiceService> logger)
        {
            _repository = repository;
            _messageRepository = messageRepository;
            _emailHistoryRepository = emailHistoryRepository;
            _settingRepository = settingRepository;
            _sellerRepository = sellerRepository;
            _templateRepository = templateRepository;
            _workingCalendarService = workingCalendarService;
            _printService = printService;
            _mailService = mailService;
            _mailSettingRepository = mailSettingRepository;
            _xmlStorageService = xmlStorageService;
            _companyDatabaseResolver = companyDatabaseResolver;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EInvoiceDto>> SearchAsync(string companyCd, EInvoiceSearchRequest request)
        {
            var pagedRequest = new EInvoiceSearchRequest
            {
                CashRegister = request.CashRegister,
                InvoiceId = request.InvoiceId,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Keyword = request.Keyword,
                Khhdon = request.Khhdon,
                KhhdonOp = request.KhhdonOp,
                ShdonFrom = request.ShdonFrom,
                ShdonTo = request.ShdonTo,
                NmuaTen = request.NmuaTen,
                NmuaTenOp = request.NmuaTenOp,
                NmuaMst = request.NmuaMst,
                NmuaMstOp = request.NmuaMstOp,
                InvoiceStatus = request.InvoiceStatus,
                CqtStatus = request.CqtStatus,
                IsSigned = request.IsSigned,
                Tchdon = request.Tchdon,
                MailStatus = request.MailStatus,
                IncludeDetails = request.IncludeDetails,
                PageNumber = 1,
                PageSize = int.MaxValue,
            };

            var paged = await SearchPagedAsync(companyCd, pagedRequest);
            return paged.Items;
        }

        public async Task<(IReadOnlyList<EInvoiceDto> Items, int TotalRecords)> SearchPagedAsync(string companyCd, EInvoiceSearchRequest request)
        {
            ValidateCompany(companyCd);
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date)
            {
                throw new ArgumentException("FromDate must be earlier than or equal to ToDate");
            }

            var pageNumber = request.InvoiceId is > 0 ? 1 : Math.Max(request.PageNumber, 1);
            var pageSize = request.InvoiceId is > 0 ? 1 : Math.Max(request.PageSize, 1);

            var pageResult = await _repository.GetHeadersPagedAsync(
                companyCd,
                request.InvoiceId,
                request.FromDate?.Date,
                request.ToDate?.Date,
                Common.NormalizeNullableText(request.Keyword),
                pageNumber,
                pageSize,
                request.IncludeDetails,
                request);

            var headers = pageResult.Items.ToList();

            return (headers.Select(EInvoiceMapper.ToDto).ToList(), pageResult.TotalRecords);
        }

        public async Task<EInvoiceDto?> GetByIdAsync(string companyCd, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var header = (await _repository.GetHeadersPagedAsync(
                companyCd,
                invoiceId,
                null,
                null,
                null,
                1,
                1,
                includeDetails: true)).Items.FirstOrDefault();
            if (header == null)
                return null;

            header.BKE_INFO = await _repository.GetBkeByInvoiceAsync(companyCd, invoiceId);
            return EInvoiceMapper.ToDto(header);
        }

        public async Task<IReadOnlyList<EInvoiceTransmissionMessageDto>> GetTransmissionMessagesAsync(string companyCd, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");

            var mtdiep = Common.NormalizeNullableText(invoice.MTDIEP);
            if (!HasText(mtdiep))
            {
                return Array.Empty<EInvoiceTransmissionMessageDto>();
            }

            var receiveMessages = await _messageRepository.GetReceiveMessagesByLookupCodeAsync(companyCd, mtdiep!);
            return receiveMessages
                .Select(message => EInvoiceTransmissionMessageMapper.FromReceive(message, invoiceId))
                .OrderBy(message => message.CREATE_AT)
                .ThenBy(message => message.MESSAGE_KEY, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> GetMailHistoryAsync(string companyCd, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");

            return await _emailHistoryRepository.GetInvoiceHistoryAsync(companyCd, invoice.INVOICE_ID);
        }

        public async Task<IReadOnlyList<EInvoiceSellerDto>> GetSellersAsync(string companyCd, EInvoiceSellerSearchRequest request)
        {
            ValidateCompany(companyCd);
            var sellers = await _sellerRepository.GetSellersAsync(
                companyCd,
                khhdon: Common.NormalizeNullableText(request.Khhdon),
                sellerId: request.SellerId,
                keyword: request.Keyword,
                includeInactive: request.IncludeInactive,
                includeAllTemplates: request.IncludeAllTemplates);
            return sellers.Select(EInvoiceMapper.ToSellerDto).ToList();
        }

        public async Task<string> GetNextBkeNoAsync(string companyCd, int? year = null)
        {
            ValidateCompany(companyCd);
            return await _repository.GetNextBkeNoAsync(companyCd, year);
        }

        public async Task<EInvoiceDto> CreateAsync(string companyCd, string userId, EInvoiceSaveRequest request)
        {
            ValidateCompany(companyCd);
            var invoice = EInvoiceMapper.ToEntity(request, companyCd);
            invoice.INVOICE_ID = 0;
            await ApplySellerDefaultsAsync(companyCd, invoice);
            if (EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
                invoice.NLAP = DateTime.UtcNow.AddHours(7).Date;
            NormalizeUnsignedInvoiceDate(invoice);
            NormalizeAndValidate(invoice);
            await ValidateBkeReferencedInvoicesAsync(companyCd, invoice);
            await EnsureBkeNumberAsync(companyCd, invoice);
            ApplyMessageCode(invoice);
            invoice.MTRACUU = null;
            ApplyLookupCode(invoice);
            await ApplySellerDefaultsAsync(companyCd, invoice);
            var id = await PersistAsync(companyCd, userId, invoice);
            _logger.LogInformation("EInvoice created. Company: {CompanyCd}, InvoiceId: {InvoiceId}", companyCd, id);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created e-invoice");
        }

        public async Task<EInvoiceDto> UpdateAsync(string companyCd, string userId, long invoiceId, EInvoiceSaveRequest request)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var existing = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");

            if (existing.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice is already signed");

            if (EInvoiceCashRegisterHelper.IsCashRegister(existing.KHHDON) && HasText(existing.SHDON))
                throw new InvalidOperationException("Hóa đơn MTT đã phát hành không được sửa.");

            if (existing.BKE_INFO?.IS_SIGNED == 1)
                throw new InvalidOperationException("Signed bảng kê cannot be modified");

            if (existing.DOC_VERSION != request.DOC_VERSION)
                throw new InvalidOperationException("E-invoice version conflict");

            var invoice = EInvoiceMapper.ToEntity(request, companyCd);
            invoice.INVOICE_ID = invoiceId;
            invoice.DOC_VERSION = request.DOC_VERSION;
            await ApplySellerDefaultsAsync(companyCd, invoice);
            if (EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
                invoice.NLAP = DateTime.UtcNow.AddHours(7).Date;
            NormalizeUnsignedInvoiceDate(invoice);
            NormalizeAndValidate(invoice);
            await ValidateBkeReferencedInvoicesAsync(companyCd, invoice);
            await EnsureBkeNumberAsync(companyCd, invoice);
            ApplyMessageCode(invoice, existing);
            invoice.MTRACUU = null;
            ApplyLookupCode(invoice, existing);
            await ApplySellerDefaultsAsync(companyCd, invoice);
            await PersistAsync(companyCd, userId, invoice);
            _logger.LogInformation("EInvoice updated. Company: {CompanyCd}, InvoiceId: {InvoiceId}, IsSigned: {IsSigned}", companyCd, invoiceId, existing.IS_SIGNED);
            return await GetByIdAsync(companyCd, invoiceId)
                ?? throw new InvalidOperationException("Failed to fetch updated e-invoice");
        }

        public async Task<EInvoiceDto> UpdateBuyerEmailAsync(string companyCd, string userId, long invoiceId, EInvoiceUpdateBuyerEmailRequest request)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");

            var buyerEmailList = EInvoiceBuyerEmailHelper.NormalizeBuyerEmailList(request.ToEmail);
            EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(buyerEmailList);

            await using (var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company))
            {
                await _repository.UpdateBuyerEmailAsync(session, companyCd, userId, invoice.INVOICE_ID, buyerEmailList);
                session.Commit();
            }

            _logger.LogInformation(
                "EInvoice buyer email updated. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                companyCd,
                invoice.INVOICE_ID);

            return await GetByIdAsync(companyCd, invoice.INVOICE_ID)
                ?? throw new InvalidOperationException("Failed to fetch updated e-invoice");
        }

        public async Task<EInvoiceDto?> LookupByCodeAsync(string companyCd, string lookupCode)
        {
            ValidateCompany(companyCd);
            var normalizedLookupCode = Common.NormalizeNullableText(lookupCode);
            if (!HasText(normalizedLookupCode))
                throw EInvoiceValidationMessages.RequiredArgument("MTRACUU");

            normalizedLookupCode = normalizedLookupCode!.ToUpperInvariant();
            var header = (await _repository.GetHeadersAsync(companyCd, null, null, null, normalizedLookupCode))
                .FirstOrDefault(x => string.Equals(Common.NormalizeNullableText(x.MTRACUU), normalizedLookupCode, StringComparison.OrdinalIgnoreCase));

            if (header == null)
                return null;

            await AttachDetailsAsync(companyCd, header);
            return EInvoiceMapper.ToDto(header);
        }

        public async Task<EInvoicePublicLookupDto?> LookupPublicAsync(string taxCode, string lookupCode)
        {
            var result = await FindPublicInvoiceAsync(taxCode, lookupCode, includeDetails: true);
            return result == null ? null : await ToPublicLookupDtoAsync(result);
        }

        public async Task<EInvoicePublicDownloadInfo?> GetPublicDownloadInfoAsync(string taxCode, string lookupCode)
        {
            var result = await FindPublicInvoiceAsync(taxCode, lookupCode, includeDetails: false);
            if (result == null)
            {
                return null;
            }

            var invoice = result.Invoice;
            return new EInvoicePublicDownloadInfo
            {
                INVOICE_ID = invoice.INVOICE_ID,
                COMPANY_CD = result.CompanyCd,
                DB_NAME = result.DatabaseName,
                MTRACUU = invoice.MTRACUU,
                KHHDON = invoice.KHHDON,
                SHDON = invoice.SHDON,
            };
        }

        public async Task<string?> GetPublicXmlAsync(string taxCode, string lookupCode)
        {
            var downloadInfo = await GetPublicDownloadInfoAsync(taxCode, lookupCode);
            if (downloadInfo == null)
            {
                return null;
            }

            var companyCd = downloadInfo.COMPANY_CD;
            var invoice = !string.IsNullOrWhiteSpace(companyCd)
                ? await GetEntityByIdAsync(companyCd, downloadInfo.INVOICE_ID, downloadInfo.DB_NAME)
                : null;
            if (invoice == null)
            {
                return null;
            }

            var storedXml = await _xmlStorageService.ResolveSignedInvoiceXmlAsync(companyCd!, invoice);
            if (HasText(storedXml))
            {
                return storedXml;
            }

            return await BuildRawXmlAsync(companyCd!, invoice);
        }

        public async Task<string> GetXmlAsync(string companyCd, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");

            var storedXml = await _xmlStorageService.ResolveSignedInvoiceXmlAsync(companyCd!, invoice);
            if (HasText(storedXml))
            {
                return storedXml!;
            }

            return await BuildRawXmlAsync(companyCd, invoice);
        }

        public async Task<EInvoiceSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");
            EInvoiceCashRegisterHelper.EnsureOrdinarySigningAllowed(invoice.KHHDON);

            if (invoice.IS_SIGNED == 1)
            {
                return new EInvoiceSigningPayloadDto
                {
                    INVOICE_ID = invoice.INVOICE_ID,
                    RAW_XML = string.Empty,
                    BKE_RAW_XML = null,
                    REQUIRES_BKE = false,
                    IS_SIGNED = true
                };
            }

            await ApplySellerDefaultsAsync(companyCd, invoice);
            await ApplySigningInvoiceDateAsync(invoice);
            await PreviewSigningSequenceAsync(companyCd, invoice);
            await EnsureMessageCodesAsync(companyCd, userId, invoice);

            var requiresBke = IsMultiRelatedTchdon(invoice.RELATED?.TCHDON);
            string? bkeRawXml = null;
            if (requiresBke)
            {
                await EnsureBkeReadyForSigningAsync(companyCd, invoice);
                bkeRawXml = BuildRawBkeXml(invoice);
            }

            var rawXml = await BuildRawXmlAsync(companyCd, invoice);

            return new EInvoiceSigningPayloadDto
            {
                INVOICE_ID = invoice.INVOICE_ID,
                RAW_XML = rawXml,
                BKE_RAW_XML = bkeRawXml,
                REQUIRES_BKE = requiresBke,
                IS_SIGNED = false
            };
        }

        public async Task<EInvoiceDto> SaveSignatureAsync(string companyCd, string userId, long invoiceId, EInvoiceSignRequest request)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            if (request == null)
                throw new ArgumentException("Request body must be provided");

            var signedXml = Common.NormalizeNullableText(request.XML);
            if (!HasText(signedXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");
            var certificateSerial=TaxDocumentSignatureValidator.GetVerifiedCertificateSerial(signedXml!,"NBan");

            var invoice = await GetEntityByIdAsync(companyCd, invoiceId)
                ?? throw new KeyNotFoundException("E-invoice not found");
            EInvoiceCashRegisterHelper.EnsureOrdinarySigningAllowed(invoice.KHHDON);

            if (invoice.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice is already signed");
            var approvedCertificate=await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Company,
                "CALL isApprovedSigningCertificate(@company,'EINVOICE',@tax,@serial)",
                new{company=companyCd,tax=invoice.SELLER_TAX_CD??"",serial=certificateSerial});
            if(approvedCertificate!=1)
                throw new InvalidOperationException("Chứng thư số chưa được CQT chấp nhận, đã ngừng sử dụng hoặc hết hiệu lực.");

            var requiresBke = IsMultiRelatedTchdon(invoice.RELATED?.TCHDON);
            var signedBkeXml = Common.NormalizeNullableText(request.BKE_XML);
            if (requiresBke)
            {
                await EnsureBkeReadyForSigningAsync(companyCd, invoice);
                if (!HasText(signedBkeXml))
                    throw EInvoiceValidationMessages.RequiredArgument("BKE_XML");
                ValidateXml(signedBkeXml!, "BKE_XML");
                ValidateSignedXmlRoot(signedBkeXml!, "BKe");
                var bkeCertificateSerial=TaxDocumentSignatureValidator.GetVerifiedCertificateSerial(signedBkeXml!,"NBan");
                if(!string.Equals(bkeCertificateSerial,certificateSerial,StringComparison.Ordinal))
                    throw new InvalidOperationException("Bảng kê phải được ký bằng cùng chứng thư số với hóa đơn.");
            }

            var originalShdon = invoice.SHDON;
            var originalNlap = invoice.NLAP;
            var messageEnqueued = false;

            try
            {
                await ApplySellerDefaultsAsync(companyCd, invoice);
                await ApplySigningInvoiceDateAsync(invoice);
                await EnsureSigningSequenceAsync(companyCd, userId, invoice);
                ValidateSignedXmlShdon(signedXml!, invoice.SHDON);
                await EnsureMessageCodesAsync(companyCd, userId, invoice);

                var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
                var packagedMessage = requiresBke
                    ? EInvoiceMessageXmlBuilder.PackageSignedInvoiceWithBke(signedXml!, signedBkeXml!, invoice, seller)
                    : EInvoiceMessageXmlBuilder.PackageSignedInvoice(signedXml!, invoice, seller);

                messageEnqueued = await SaveSignedXmlAsync(
                    companyCd,
                    userId,
                    invoiceId,
                    signedXml!,
                    packagedMessage,
                    invoice.MTDIEP,
                    invoice.NLAP,
                    requiresBke ? signedBkeXml : null);

                _logger.LogInformation(
                    "EInvoice signed and packaged. Company: {CompanyCd}, InvoiceId: {InvoiceId}, MessageType: {MessageType}, CertificateThumbprint: {CertificateThumbprint}",
                    companyCd,
                    invoiceId,
                    packagedMessage.MLTDIEP,
                    request.CERTIFICATE_THUMBPRINT);

                return await GetByIdAsync(companyCd, invoiceId)
                    ?? throw new InvalidOperationException("Failed to fetch signed e-invoice");
            }
            catch (Exception ex)
            {
                if (!messageEnqueued)
                {
                    await TryRevertSigningReservationAsync(
                        companyCd,
                        userId,
                        invoiceId,
                        originalShdon,
                        originalNlap,
                        invoice.MTDIEP,
                        ex);
                }

                throw;
            }
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, long invoiceId)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");

            await EnsureDeletableAsync(companyCd, invoiceId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.DeletePxkByInvoiceAsync(session, companyCd, invoiceId, userId);
                await _repository.DeleteBkeByInvoiceAsync(session, companyCd, invoiceId, userId);
                var affected = await _repository.DeleteAsync(session, companyCd, invoiceId, userId);
                session.Commit();
                _logger.LogInformation("EInvoice deleted. Company: {CompanyCd}, InvoiceId: {InvoiceId}", companyCd, invoiceId);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> invoiceIds)
        {
            ValidateCompany(companyCd);
            var ids = invoiceIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("InvoiceIds");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affected = 0;
                foreach (var id in ids)
                {
                    await EnsureDeletableAsync(companyCd, id);
                    await _repository.DeletePxkByInvoiceAsync(session, companyCd, id, userId);
                    await _repository.DeleteBkeByInvoiceAsync(session, companyCd, id, userId);
                    affected += await _repository.DeleteAsync(session, companyCd, id, userId);
                }

                session.Commit();
                _logger.LogInformation("EInvoice batch deleted. Company: {CompanyCd}, Count: {Count}", companyCd, ids.Count);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<EInvoiceSendMailResult> SendMailAsync(
            string companyCd,
            string userId,
            EInvoiceSendMailRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateCompany(companyCd);
            if (request?.Items == null || request.Items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var items = request.Items
                .Where(x => x.InvoiceId > 0)
                .GroupBy(x => x.InvoiceId)
                .Select(group => group.Last())
                .ToList();

            if (items.Count == 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("Items");
            }

            var emailTemplate = await _templateRepository.GetActiveTemplateAsync(
                EInvoiceMailTemplateRenderer.EmailTemplateType,
                EInvoiceMailTemplateRenderer.SendInvoiceTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice email template '{EInvoiceMailTemplateRenderer.SendInvoiceTemplateCd}' is not configured");

            var mailSetting = await _mailSettingRepository.GetActiveSettingAsync(companyCd, EInvoiceMailCode);
            var mailOptions = _mailService.ParseOptions(mailSetting?.CONFIG_JSON);
            var databaseName = await _companyDatabaseResolver.ResolveDatabaseNameAsync(companyCd);

            var result = new EInvoiceSendMailResult();

            foreach (var itemRequest in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = new EInvoiceSendMailResultItem { InvoiceId = itemRequest.InvoiceId };
                var historyToEmail = Common.NormalizeNullableText(itemRequest.ToEmail) ?? string.Empty;
                var historySubject = string.Empty;
                var historyBody = string.Empty;
                var historyAttachmentInfo = string.Empty;
                try
                {
                    var buyerEmailList = EInvoiceBuyerEmailHelper.NormalizeBuyerEmailList(itemRequest.ToEmail);
                    if (string.IsNullOrWhiteSpace(buyerEmailList))
                    {
                        throw new ArgumentException("Recipient email is required");
                    }

                    EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(buyerEmailList);
                    historyToEmail = buyerEmailList;

                    var invoice = await GetEntityByIdAsync(companyCd, itemRequest.InvoiceId)
                        ?? throw new KeyNotFoundException("E-invoice not found");

                    EnsureInvoiceReadyToSendMail(invoice);

                    var signedXml = await _xmlStorageService.ResolveSignedInvoiceXmlAsync(companyCd, invoice, cancellationToken);
                    if (!HasText(signedXml))
                    {
                        throw new InvalidOperationException("Signed invoice XML is not available on FTP storage");
                    }

                    await using (var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company))
                    {
                        await _repository.UpdateBuyerEmailAsync(session, companyCd, userId, itemRequest.InvoiceId, buyerEmailList);
                        session.Commit();
                    }

                    invoice.NMUA_DCTDTU = buyerEmailList;

                    var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
                    var displayNo = BuildInvoiceDisplayNo(invoice);
                    var renderedMail = EInvoiceMailTemplateRenderer.Render(
                        emailTemplate,
                        invoice,
                        seller,
                        displayNo,
                        EInvoiceMailRenderOptions.FromConfiguration(_configuration));
                    historySubject = renderedMail.Subject;
                    historyBody = renderedMail.Body;
                    var baseFileName = SanitizeFileName($"EInvoice_{displayNo}");
                    var attachments = await BuildInvoiceMailAttachmentsAsync(
                        companyCd,
                        itemRequest.InvoiceId,
                        signedXml!,
                        mailOptions,
                        baseFileName,
                        cancellationToken);
                    historyAttachmentInfo = BuildMailAttachmentInfo(attachments);

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

                    item.Success = true;
                    item.ToEmail = buyerEmailList;
                    item.Message = "Sent";
                    result.Sent++;
                    await TrySetInvoiceMailStatusAsync(
                        companyCd,
                        userId,
                        itemRequest.InvoiceId,
                        EInvoiceMailStatusSent);
                    await TryInsertInvoiceEmailHistoryAsync(
                        companyCd,
                        databaseName,
                        userId,
                        itemRequest.InvoiceId,
                        mailSetting?.FROM_EMAIL,
                        historyToEmail,
                        mailOptions.Cc,
                        mailOptions.Bcc,
                        historySubject,
                        historyBody,
                        historyAttachmentInfo,
                        "SENT",
                        null);
                }
                catch (Exception ex)
                {
                    item.Success = false;
                    item.Message = ex.Message;
                    result.Skipped++;
                    await TrySetInvoiceMailStatusAsync(
                        companyCd,
                        userId,
                        itemRequest.InvoiceId,
                        EInvoiceMailStatusError);
                    await TryInsertInvoiceEmailHistoryAsync(
                        companyCd,
                        databaseName,
                        userId,
                        itemRequest.InvoiceId,
                        mailSetting?.FROM_EMAIL,
                        historyToEmail,
                        mailOptions.Cc,
                        mailOptions.Bcc,
                        historySubject,
                        historyBody,
                        historyAttachmentInfo,
                        "ERROR",
                        ex.Message);
                    _logger.LogWarning(
                        ex,
                        "EInvoice send mail failed. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                        companyCd,
                        itemRequest.InvoiceId);
                }

                result.Results.Add(item);
            }

            if (result.Sent == 0)
            {
                var firstError = result.Results.FirstOrDefault(x => !x.Success)?.Message;
                throw new InvalidOperationException(firstError ?? "Unable to send e-invoice mail");
            }

            return result;
        }

        private async Task TrySetInvoiceMailStatusAsync(
            string companyCd,
            string userId,
            long invoiceId,
            int mailStatus)
        {
            try
            {
                await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
                try
                {
                    await _repository.SetMailStatusAsync(session, companyCd, userId, invoiceId, mailStatus);
                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to update e-invoice mail status. Company: {CompanyCd}, InvoiceId: {InvoiceId}, MailStatus: {MailStatus}",
                    companyCd,
                    invoiceId,
                    mailStatus);
            }
        }

        private async Task TryInsertInvoiceEmailHistoryAsync(
            string companyCd,
            string databaseName,
            string userId,
            long invoiceId,
            string? fromEmail,
            string toEmail,
            string? ccEmail,
            string? bccEmail,
            string subject,
            string body,
            string attachmentInfo,
            string sendStatus,
            string? errorMessage)
        {
            try
            {
                await _emailHistoryRepository.InsertAsync(new EInvoiceEmailHistoryCreateRequest
                {
                    COMPANY_CD = companyCd,
                    DB_NAME = databaseName,
                    REF_TYPE = EInvoiceEmailHistoryRefTypes.Invoice,
                    REF_ID = invoiceId,
                    SEND_TYPE = "EMAIL",
                    SEND_STATUS = sendStatus,
                    FROM_EMAIL = fromEmail ?? string.Empty,
                    TO_EMAIL = toEmail,
                    CC_EMAIL = ccEmail ?? string.Empty,
                    BCC_EMAIL = bccEmail ?? string.Empty,
                    MAIL_SUBJECT = subject,
                    MAIL_BODY = body,
                    ATTACHMENT_INFO = attachmentInfo,
                    RETRY_COUNT = 0,
                    ERROR_MESSAGE = errorMessage ?? string.Empty,
                    SEND_DT = string.Equals(sendStatus, "SENT", StringComparison.OrdinalIgnoreCase) ? DateTime.Now : null,
                    CREATE_BY = userId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to write e-invoice email history. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                    companyCd,
                    invoiceId);
            }
        }

        private static string BuildMailAttachmentInfo(IReadOnlyList<MailAttachmentDto> attachments)
        {
            if (attachments.Count == 0)
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(attachments.Select(attachment => new
            {
                attachment.FileName,
                attachment.ContentType,
                Size = attachment.Content?.Length ?? 0
            }));
        }

        private static void EnsureInvoiceReadyToSendMail(EInvoiceInfo invoice)
        {
            if (invoice.IS_SIGNED != 1)
            {
                throw new InvalidOperationException("E-invoice is not signed");
            }

            if (!HasText(Common.NormalizeNullableText(invoice.SHDON)))
            {
                throw new InvalidOperationException("E-invoice number (SHDON) is not assigned");
            }

            if (!HasText(Common.NormalizeNullableText(invoice.MTRACUU)))
            {
                throw new InvalidOperationException("E-invoice lookup code (MTRACUU) is not assigned");
            }
        }

        private static string BuildInvoiceDisplayNo(EInvoiceInfo invoice)
        {
            var parts = new[]
            {
                Common.NormalizeNullableText(invoice.KHMSHDON),
                Common.NormalizeNullableText(invoice.KHHDON),
                Common.NormalizeNullableText(invoice.SHDON),
            }.Where(HasText).ToArray();

            return parts.Length > 0 ? string.Join(string.Empty, parts) : $"#{invoice.INVOICE_ID}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "EInvoice.pdf" : sanitized;
        }

        private async Task<List<MailAttachmentDto>> BuildInvoiceMailAttachmentsAsync(
            string companyCd,
            long invoiceId,
            string signedXml,
            MailSendOptions mailOptions,
            string baseFileName,
            CancellationToken cancellationToken)
        {
            var attachments = new List<MailAttachmentDto>();

            if (mailOptions.AttachPdf)
            {
                var pdfBytes = await _printService.ExportPdfFromSignedXmlContentAsync(
                    companyCd,
                    invoiceId,
                    signedXml,
                    cancellationToken);
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

        private async Task<EInvoiceInfo?> GetEntityByIdAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var invoice = (await _repository.GetHeadersAsync(companyCd, invoiceId, null, null, null, dbName)).FirstOrDefault();
            if (invoice == null)
                return null;

            await AttachDetailsAsync(companyCd, invoice, dbName);
            return invoice;
        }

        private async Task<PublicInvoiceLookupResult?> FindPublicInvoiceAsync(string taxCode, string lookupCode, bool includeDetails)
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
                            target.DB_NAME))
                        .FirstOrDefault(x =>
                            string.Equals(NormalizeLookupCode(x.MTRACUU), normalizedLookupCode, StringComparison.OrdinalIgnoreCase) &&
                            TaxCodeMatchesInvoice(normalizedTaxCode!, x));

                    if (header == null)
                    {
                        continue;
                    }

                    if (includeDetails)
                    {
                        await AttachDetailsAsync(target.COMPANY_CD, header, target.DB_NAME);
                    }

                    return new PublicInvoiceLookupResult(target.COMPANY_CD, target.DB_NAME, header);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Public e-invoice lookup skipped company after database query failed. Company: {CompanyCd}, DbName: {DbName}",
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

        private static bool TaxCodeMatchesInvoice(string normalizedTaxCode, EInvoiceInfo invoice)
        {
            return string.Equals(NormalizeTaxCode(invoice.SELLER_TAX_CD), normalizedTaxCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeTaxCode(invoice.NMUA_MST), normalizedTaxCode, StringComparison.OrdinalIgnoreCase);
        }

        private async Task AttachDetailsAsync(string companyCd, EInvoiceInfo invoice, string? dbName = null)
        {
            var bundle = await _repository.GetDetailsBundleAsync(companyCd, new[] { invoice.INVOICE_ID }, dbName);
            var specialByDetailId = bundle.Specials.ToDictionary(x => x.DETAIL_ID);

            foreach (var detail in bundle.Details)
            {
                if (specialByDetailId.TryGetValue(detail.DETAIL_ID, out var special))
                {
                    detail.SPECIAL = special;
                }
            }

            invoice.DETAILS = bundle.Details.ToList();
            invoice.PXK_INFO = bundle.PxkItems.FirstOrDefault(x => x.INVOICE_ID == invoice.INVOICE_ID);
            invoice.RELATED = bundle.RelatedItems.FirstOrDefault(x => x.INVOICE_ID == invoice.INVOICE_ID);
            invoice.BKE_INFO = await _repository.GetBkeByInvoiceAsync(companyCd, invoice.INVOICE_ID, dbName);
        }

        private sealed class PublicLookupCompanyTarget
        {
            public string COMPANY_CD { get; set; } = string.Empty;
            public string DB_NAME { get; set; } = string.Empty;
        }

        private sealed record PublicInvoiceLookupResult(string CompanyCd, string DatabaseName, EInvoiceInfo Invoice);

        private async Task ApplySellerDefaultsAsync(string companyCd, EInvoiceInfo invoice)
        {
            var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
            ApplySeller(invoice, seller);
        }

        private async Task<EInvoiceSellerInfo> ResolveSellerForInvoiceAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (invoice.XSL_ID is > 0)
            {
                var matched = (await _sellerRepository.GetSellersAsync(
                    companyCd,
                    sellerId: invoice.SELLER_ID,
                    xslId: invoice.XSL_ID,
                    includeInactive: true)).FirstOrDefault();
                if (matched != null)
                    return matched;

                throw new KeyNotFoundException("E-invoice XSL template not found");
            }

            if (invoice.SELLER_ID is > 0)
            {
                var matched = (await _sellerRepository.GetSellersAsync(
                    companyCd,
                    khhdon: invoice.KHHDON,
                    sellerId: invoice.SELLER_ID,
                    includeInactive: true)).FirstOrDefault();
                if (matched != null)
                    return matched;
            }

            return await ResolveSellerAsync(companyCd, invoice.KHHDON);
        }

        private async Task<EInvoiceSellerInfo> ResolveSellerAsync(string companyCd, string? khhdon)
        {
            var normalizedKhhdon = Common.NormalizeNullableText(khhdon);
            var sellers = (await _sellerRepository.GetSellersAsync(
                companyCd,
                khhdon: normalizedKhhdon,
                includeInactive: false)).ToList();
            if (sellers.Count == 0 && HasText(normalizedKhhdon))
                throw new KeyNotFoundException("E-invoice seller not found");

            if (sellers.Count == 0)
                throw new InvalidOperationException("E-invoice seller is not configured");

            return sellers.FirstOrDefault(x => x.XSL_IS_DEFAULT == 1) ?? sellers[0];
        }

        private static void ApplySeller(EInvoiceInfo invoice, EInvoiceSellerInfo seller)
        {
            invoice.SELLER_ID = seller.SELLER_ID;
            invoice.XSL_ID = seller.XSL_ID > 0 ? seller.XSL_ID : invoice.XSL_ID;
            invoice.THDON = Common.NormalizeNullableText(seller.THDON) ?? invoice.THDON;
            invoice.KHMSHDON = Common.NormalizeNullableText(seller.KHMSHDON) ?? invoice.KHMSHDON;
            invoice.KHHDON = Common.NormalizeNullableText(seller.KHHDON) ?? invoice.KHHDON;
            if (EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
                invoice.PBAN = "2.1.1";
        }

        private static void NormalizeUnsignedInvoiceDate(EInvoiceInfo invoice)
        {
            if (invoice.IS_SIGNED != 1 && EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON) && !HasText(invoice.SHDON))
            {
                invoice.MCCQT = null;
                invoice.NLAP = DateTime.UtcNow.AddHours(7).Date;
                return;
            }
            if (invoice.IS_SIGNED == 1)
            {
                return;
            }

            if (invoice.NLAP.HasValue)
            {
                invoice.NLAP = invoice.NLAP.Value.Date;
                if (invoice.NLAP.Value.Date > DateTime.Today)
                {
                    throw new ArgumentException("NLAP cannot be later than today");
                }
            }
        }

        private async Task ApplySigningInvoiceDateAsync(EInvoiceInfo invoice)
        {
            if (EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON))
            {
                invoice.NLAP = DateTime.UtcNow.AddHours(7).Date;
                return;
            }
            if (!invoice.NLAP.HasValue)
            {
                throw EInvoiceValidationMessages.RequiredArgument("NLAP");
            }

            invoice.NLAP = invoice.NLAP.Value.Date;
            if (invoice.NLAP.Value.Date > DateTime.Today)
            {
                throw new ArgumentException("NLAP cannot be later than today");
            }

            await ValidateSigningDeadlineAsync(invoice);
        }

        private async Task ValidateSigningDeadlineAsync(EInvoiceInfo invoice)
        {
            if (!invoice.NLAP.HasValue)
            {
                throw EInvoiceValidationMessages.RequiredArgument("NLAP");
            }

            var signDate = DateTime.Today;
            var deadline = await _workingCalendarService.GetNextWorkingDateAsync(invoice.NLAP.Value, 1);
            if (signDate > deadline)
            {
                throw new InvalidOperationException(
                    "Đã quá hạn ký/gửi hóa đơn theo ngày làm việc tiếp theo của ngày lập hóa đơn.");
            }
        }

        private async Task PreviewSigningSequenceAsync(string companyCd, EInvoiceInfo invoice)
        {
            var sequence = await ResolveSigningSequenceAsync(companyCd, invoice);
            invoice.SHDON = sequence.NextShdon;
        }

        private async Task EnsureSigningSequenceAsync(string companyCd, string userId, EInvoiceInfo invoice)
        {
            var sequence = await ResolveSigningSequenceAsync(companyCd, invoice);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetShdonAsync(session, companyCd, userId, invoice.INVOICE_ID, sequence.NextShdon, invoice.NLAP!.Value);
                session.Commit();
                invoice.SHDON = sequence.NextShdon;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task<EInvoiceSigningSequence> ResolveSigningSequenceAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (!invoice.NLAP.HasValue)
                throw EInvoiceValidationMessages.RequiredArgument("NLAP");

            await ApplySellerDefaultsAsync(companyCd, invoice);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            var sequence = await _repository.GetSigningSequenceAsync(
                session,
                companyCd,
                invoice.INVOICE_ID,
                invoice.KHMSHDON,
                invoice.KHHDON);

            if (sequence.LastNlap.HasValue && invoice.NLAP.Value.Date < sequence.LastNlap.Value.Date)
            {
                throw new InvalidOperationException(
                    "Invoice date must be greater than or equal to the previous invoice date in the same serial.");
            }

            return sequence;
        }

        private static void ValidateSignedXmlShdon(string signedXml, string? expectedShdon)
        {
            if (!HasText(expectedShdon))
                throw EInvoiceValidationMessages.RequiredArgument("SHDON");

            var document = new System.Xml.XmlDocument();
            document.LoadXml(signedXml);
            var shdonNode = document.SelectSingleNode("//*[local-name()='SHDon' or local-name()='SHDON']");
            var signedShdon = Common.NormalizeNullableText(shdonNode?.InnerText);

            if (!string.Equals(signedShdon, expectedShdon, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Signed XML invoice number does not match the assigned SHDON. Please sign again.");
            }
        }

        private async Task<bool> SaveSignedXmlAsync(
            string companyCd,
            string userId,
            long invoiceId,
            string signedXml,
            EInvoicePackagedMessage packagedMessage,
            string? mtdiep,
            DateTime? invoiceDate,
            string? signedBkeXml = null)
        {
            ValidateXml(signedXml, "XML");
            ValidateXml(packagedMessage.RequestXml, "PACKAGE_XML");
            if (HasText(signedBkeXml))
            {
                ValidateXml(signedBkeXml!, "BKE_XML");
            }

            var xmlFtpPath = await _xmlStorageService.UploadInvoiceXmlAsync(
                companyCd,
                invoiceId,
                signedXml,
                invoiceDate);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetSignatureAsync(session, companyCd, userId, invoiceId, xmlFtpPath, mtdiep, 1, null);
                if (HasText(signedBkeXml))
                {
                    await _repository.SetBkeSignatureAsync(session, companyCd, userId, invoiceId, signedBkeXml!, 1);
                }
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
                EInvoiceMessageTargetTypes.Invoice,
                invoiceId,
                packagedMessage);

            return true;
        }

        private async Task TryRevertSigningReservationAsync(
            string companyCd,
            string userId,
            long invoiceId,
            string? originalShdon,
            DateTime? originalNlap,
            string? mtdiep,
            Exception failure)
        {
            try
            {
                if (await _messageRepository.SendExistsAsync(companyCd, mtdiep ?? string.Empty, invoiceId))
                {
                    return;
                }

                await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
                try
                {
                    await _repository.RevertSigningReservationAsync(
                        session,
                        companyCd,
                        userId,
                        invoiceId,
                        originalShdon,
                        originalNlap);
                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }

                _logger.LogWarning(
                    failure,
                    "Reverted e-invoice signing reservation after failure before outbound message enqueue. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                    companyCd,
                    invoiceId);
            }
            catch (Exception revertEx)
            {
                _logger.LogError(
                    revertEx,
                    "Failed to revert e-invoice signing reservation. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                    companyCd,
                    invoiceId);
            }
        }

        private async Task<long> PersistAsync(string companyCd, string userId, EInvoiceInfo invoice)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var invoiceId = await _repository.SetHeaderAsync(session, companyCd, userId, invoice);
                await _repository.DeleteDetailsByInvoiceAsync(session, companyCd, invoiceId, userId);
                await _repository.DeleteDetailSpecialByInvoiceAsync(session, companyCd, invoiceId);

                var activeDetails = invoice.DETAILS.Where(x => x.ISDEL != 1).ToList();
                for (var index = 0; index < activeDetails.Count; index++)
                {
                    var detail = activeDetails[index];
                    detail.DETAIL_ID = 0;
                    detail.INVOICE_ID = invoiceId;
                    detail.COMPANY_CD = companyCd;
                    detail.STT ??= index + 1;
                    var detailId = await _repository.SetDetailAsync(session, companyCd, userId, invoiceId, detail);
                    await PersistDetailSpecialAsync(session, companyCd, userId, invoiceId, detailId, detail);
                }

                if (HasRelatedData(invoice.RELATED))
                {
                    var related = invoice.RELATED!;
                    related.INVOICE_ID = invoiceId;
                    related.COMPANY_CD = companyCd;
                    related.RELATED_ID = await _repository.SetRelatedAsync(session, companyCd, userId, invoiceId, related);
                }
                else
                {
                    await _repository.DeleteRelatedByInvoiceAsync(session, companyCd, invoiceId);
                }

                if (IsMultiRelatedTchdon(invoice.RELATED?.TCHDON) && HasBkeData(invoice.BKE_INFO))
                {
                    await PersistBkeAsync(session, companyCd, userId, invoiceId, invoice);
                }
                else
                {
                    await _repository.DeleteBkeByInvoiceAsync(session, companyCd, invoiceId, userId);
                }

                if (EInvoiceWarehouseHelper.IsWarehouseForm(invoice.KHMSHDON))
                {
                    var pxk = BuildPxkInfo(invoice);
                    pxk.INVOICE_ID = invoiceId;
                    pxk.PXK_ID = await _repository.SetPxkAsync(session, companyCd, userId, invoiceId, pxk);
                }
                else
                {
                    await _repository.DeletePxkByInvoiceAsync(session, companyCd, invoiceId, userId);
                }

                session.Commit();
                return invoiceId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void NormalizeAndValidate(EInvoiceInfo invoice)
        {
            invoice.PBAN = Common.NormalizeNullableText(invoice.PBAN) ?? "2.1.0";
            invoice.DVTTE = (Common.NormalizeNullableText(invoice.DVTTE) ?? "VND").ToUpperInvariant();
            invoice.IS_SIGNED = invoice.IS_SIGNED == 1 ? 1 : 0;
            var isForeignCurrency = !string.Equals(invoice.DVTTE, "VND", StringComparison.OrdinalIgnoreCase);
            if (!isForeignCurrency)
            {
                invoice.TGIA = 1m;
            }
            else if (invoice.TGIA is null or <= 0m)
            {
                throw EInvoiceValidationMessages.RequiredArgument("TGIA");
            }

            invoice.TGTCTHUE ??= 0m;
            invoice.TGTKCTHUE ??= 0m;
            invoice.TGTTTHUE ??= 0m;
            invoice.TTCKTMAI ??= 0m;
            invoice.TGTKHAC ??= 0m;
            invoice.TGTTTBSO ??= 0m;
            invoice.TGTCTHUE_VND ??= 0m;
            invoice.TGTKCTHUE_VND ??= 0m;
            invoice.TGTTTHUE_VND ??= 0m;
            invoice.TTCKTMAI_VND ??= 0m;
            invoice.TGTKHAC_VND ??= 0m;
            invoice.TGTTTBSO_VND ??= 0m;

            if (!HasText(invoice.MSTTCGP))
                invoice.MSTTCGP = EInvoiceMessageXmlBuilder.MessageCodePrefix;

            if (invoice.IS_SIGNED != 1)
                invoice.SHDON = null;

            if (invoice.IS_SIGNED == 1 && !invoice.NLAP.HasValue)
                throw EInvoiceValidationMessages.RequiredArgument("NLAP");

            if (invoice.SELLER_ID is null or <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("SELLER_ID");

            if (invoice.DVTTE.Length != 3)
                throw new ArgumentException("DVTTE must contain 3 characters");

            if (invoice.TGIA <= 0)
                throw new ArgumentException("TGIA must be greater than zero");

            EInvoiceWarehouseHelper.ValidateWarehouseInvoice(invoice);

            var relatedTchdon = invoice.RELATED?.TCHDON ?? 0;
            invoice.TCHDON = relatedTchdon;
            if (relatedTchdon is not (1 or 2) || invoice.RELATED?.IS_EXTERNAL == 1)
            {
                invoice.SOURCE_INVOICE_ID = null;
            }

            SyncAndValidateBke(invoice);

            var activeDetails = invoice.DETAILS.Where(x => x.ISDEL != 1).ToList();
            if (activeDetails.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("DETAIL_LINE");

            for (var index = 0; index < activeDetails.Count; index++)
            {
                var detail = activeDetails[index];
                detail.STT ??= index + 1;
                detail.STCKHAU ??= 0m;
                detail.THTIEN ??= 0m;
                detail.STCKHAU_VND ??= 0m;
                detail.THTIEN_VND ??= 0m;
                if (!isForeignCurrency)
                {
                    detail.DGIA_VND = detail.DGIA ?? detail.DGIA_VND;
                    detail.STCKHAU_VND = detail.STCKHAU;
                    detail.THTIEN_VND = detail.THTIEN;
                }

                if (string.IsNullOrWhiteSpace(Common.NormalizeNullableText(detail.THHDVU)))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("THHDVU", detail.STT ?? (index + 1));
            }
        }

        private async Task EnsureMessageCodesAsync(string companyCd, string userId, EInvoiceInfo invoice)
        {
            var hadMtdiep = HasText(invoice.MTDIEP);
            var hadLookupCode = HasText(invoice.MTRACUU);
            ApplyMessageCode(invoice);
            ApplyLookupCode(invoice);

            if (hadMtdiep && hadLookupCode)
                return;

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetHeaderAsync(session, companyCd, userId, invoice);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void ApplyMessageCode(EInvoiceInfo invoice, EInvoiceInfo? existing = null)
        {
            invoice.MTDIEP = Common.NormalizeNullableText(invoice.MTDIEP)
                ?? Common.NormalizeNullableText(existing?.MTDIEP);

            if (!HasText(invoice.MTDIEP))
                invoice.MTDIEP = GenerateEInvoiceMtdiep();

            invoice.MGDDTu = Common.NormalizeNullableText(invoice.MGDDTu)
                ?? Common.NormalizeNullableText(existing?.MGDDTu);
        }

        private static string GenerateEInvoiceMtdiep()
        {
            return EInvoiceMessageXmlBuilder.GenerateMessageCode();
        }

        private static void ApplyLookupCode(EInvoiceInfo invoice, EInvoiceInfo? existing = null)
        {
            invoice.MTRACUU = Common.NormalizeNullableText(invoice.MTRACUU)
                ?? Common.NormalizeNullableText(existing?.MTRACUU);

            invoice.MTRACUU = HasText(invoice.MTRACUU)
                ? invoice.MTRACUU!.ToUpperInvariant()
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

            return $"TC{new string(chars)}";
        }

        private static EInvoicePublicLookupDto ToPublicLookupDto(EInvoiceInfo invoice)
        {
            return new EInvoicePublicLookupDto
            {
                MTRACUU = invoice.MTRACUU,
                THDON = invoice.THDON,
                KHMSHDON = invoice.KHMSHDON,
                KHHDON = invoice.KHHDON,
                SHDON = invoice.SHDON,
                NLAP = invoice.NLAP,
                DVTTE = invoice.DVTTE,
                SELLER_NM = invoice.SELLER_NM,
                SELLER_TAX_CD = invoice.SELLER_TAX_CD,
                NMUA_TEN = invoice.NMUA_TEN,
                NMUA_MST = invoice.NMUA_MST,
                NMUA_DCHI = invoice.NMUA_DCHI,
                TGTCTHUE = invoice.TGTCTHUE,
                TGTTTHUE = invoice.TGTTTHUE,
                TTCKTMAI = invoice.TTCKTMAI,
                TGTTTBSO = invoice.TGTTTBSO,
                TGTTTBCHU = invoice.TGTTTBCHU,
                MCCQT = invoice.MCCQT,
                DETAILS = invoice.DETAILS
                    .Where(x => x.ISDEL != 1)
                    .OrderBy(x => x.STT ?? int.MaxValue)
                    .Select(ToPublicDetailDto)
                    .ToList(),
            };
        }

        private async Task<EInvoicePublicLookupDto> ToPublicLookupDtoAsync(PublicInvoiceLookupResult result)
        {
            var dto = ToPublicLookupDto(result.Invoice);

            try
            {
                dto.HTML = await ExecuteWithCompanyContextAsync(
                    result.CompanyCd,
                    () => _printService.GetHtmlAsync(result.CompanyCd, result.Invoice.INVOICE_ID));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Public e-invoice HTML preview failed. Company: {CompanyCd}, InvoiceId: {InvoiceId}",
                    result.CompanyCd,
                    result.Invoice.INVOICE_ID);
                dto.HTML = null;
            }

            return dto;
        }

        private async Task<T> ExecuteWithCompanyContextAsync<T>(string companyCd, Func<Task<T>> action)
        {
            if (!CompanyRouteContextMiddleware.IsValidCompanyCode(companyCd))
            {
                return await action();
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return await action();
            }

            var hadCompany = CompanyRouteContextMiddleware.TryGetCompanyCode(httpContext, out var previousCompanyCd);
            httpContext.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = companyCd.Trim();
            try
            {
                return await action();
            }
            finally
            {
                if (hadCompany)
                {
                    httpContext.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = previousCompanyCd;
                }
                else
                {
                    httpContext.Items.Remove(CompanyRouteContextMiddleware.CompanyCdItemKey);
                }
            }
        }

        private static EInvoicePublicDetailDto ToPublicDetailDto(EInvoiceDetail detail)
        {
            return new EInvoicePublicDetailDto
            {
                STT = detail.STT,
                TCHAT = detail.TCHAT,
                MHHDVU = detail.MHHDVU,
                THHDVU = detail.THHDVU,
                DVTINH = detail.DVTINH,
                SLUONG = detail.SLUONG,
                DGIA = detail.DGIA,
                STCKHAU = detail.STCKHAU,
                THTIEN = detail.THTIEN,
                TSUAT = detail.TSUAT,
            };
        }

        private static string? NormalizeLookupCode(string? value)
        {
            var text = Common.NormalizeNullableText(value);
            if (!HasText(text))
            {
                return null;
            }

            return new string(text!.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }

        private static string? NormalizeTaxCode(string? value)
        {
            var text = Common.NormalizeNullableText(value);
            if (!HasText(text))
            {
                return null;
            }

            var normalized = new string(text!
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

            return HasText(normalized) ? normalized : null;
        }

        private static bool IsSafeLookupCode(string value)
        {
            return value.Length is >= 8 and <= 100 && value.All(char.IsLetterOrDigit);
        }

        private Task PersistDetailSpecialAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            long detailId,
            EInvoiceDetail detail)
        {
            if (detail.TCHAT != 5 || !HasDetailSpecialData(detail.SPECIAL))
            {
                return Task.CompletedTask;
            }

            var special = detail.SPECIAL!;
            special.SPECIAL_ID = 0;
            special.INVOICE_ID = invoiceId;
            special.DETAIL_ID = detailId;
            special.COMPANY_CD = companyCd;
            return _repository.SetDetailSpecialAsync(session, companyCd, userId, invoiceId, detailId, special);
        }

        private static bool HasDetailSpecialData(EInvoiceDetailSpecialInfo? special)
        {
            if (special == null || special.LHHDTRUNG is not (1 or 2 or 3 or 4))
            {
                return false;
            }

            if (HasText(special.EXTRA_JSON))
            {
                return true;
            }

            return special.LHHDTRUNG switch
            {
                1 => HasText(special.SKHUNG) && HasText(special.SMAY),
                2 => HasText(special.BKSPT_VCHUYEN),
                3 => HasText(special.TNG_HANG)
                    || HasText(special.DCNG_HANG)
                    || HasText(special.MSTNG_HANG)
                    || HasText(special.MDDNG_HANG),
                4 => false,
                _ => false
            };
        }

        private static bool HasRelatedData(EInvoiceRelatedInfo? related)
        {
            if (related == null || related.TCHDON is not (1 or 2 or 3 or 4))
                return false;

            // Multi adjust/replace: TCHDon 3/4 alone is enough to keep RELATED row.
            if (related.TCHDON is 3 or 4)
                return true;

            return HasText(related.KHMSHDCLQUAN)
                || HasText(related.KHHDCLQUAN)
                || HasText(related.SHDCLQUAN)
                || related.LHDCLQUAN.HasValue
                || related.NLHDCLQUAN.HasValue
                || HasText(related.MSTCLQUAN)
                || related.LDDCTTHE.HasValue
                || HasText(related.SBKCLQUAN)
                || related.NBKCLQUAN.HasValue
                || HasText(related.GCHU);
        }

        private static bool IsMultiRelatedTchdon(int? tchdon)
        {
            return tchdon is 3 or 4;
        }

        private static bool HasBkeData(EInvoiceBkeInfo? bke)
        {
            if (bke == null || bke.ISDEL == 1)
                return false;

            return HasText(bke.SBKE)
                || bke.NBKE.HasValue
                || bke.REASONS.Any(x => x.ISDEL != 1 && HasText(x.LDO))
                || bke.DETAILS.Any(x => x.ISDEL != 1);
        }

        private static void SyncAndValidateBke(EInvoiceInfo invoice)
        {
            var related = invoice.RELATED;
            var isMulti = IsMultiRelatedTchdon(related?.TCHDON);
            if (!isMulti)
            {
                invoice.BKE_INFO = null;
                return;
            }

            if (related == null)
                throw EInvoiceValidationMessages.RequiredArgument("RELATED");

            var bke = invoice.BKE_INFO ?? new EInvoiceBkeInfo();
            invoice.BKE_INFO = bke;

            var sbke = Common.NormalizeNullableText(bke.SBKE)
                ?? Common.NormalizeNullableText(related.SBKCLQUAN);
            var nbke = bke.NBKE ?? related.NBKCLQUAN ?? DateTime.Today;

            // Số bảng kê có thể để trống tạm — EnsureBkeNumberAsync sẽ cấp trước khi Persist.
            bke.NBKE = nbke.Date;
            related.NBKCLQUAN = bke.NBKE;
            if (HasText(sbke))
            {
                bke.SBKE = sbke!;
                related.SBKCLQUAN = bke.SBKE;
            }
            else
            {
                bke.SBKE = string.Empty;
                related.SBKCLQUAN = null;
            }

            invoice.SBKE = related.SBKCLQUAN;
            invoice.NBKE = related.NBKCLQUAN;

            bke.TCHDON = related.TCHDON == 3 ? 1 : 2;
            bke.PBAN = Common.NormalizeNullableText(bke.PBAN) ?? "2.1.1";
            bke.KHMBKE = Common.NormalizeNullableText(bke.KHMBKE) ?? "01/BK-ĐCTT";
            bke.TBKE = Common.NormalizeNullableText(bke.TBKE)
                ?? (bke.TCHDON == 1
                    ? "Bảng kê hóa đơn điện tử bị thay thế"
                    : "Bảng kê hóa đơn điện tử bị điều chỉnh");
            bke.SELLER_ID ??= invoice.SELLER_ID;
            bke.NBAN = Common.NormalizeNullableText(invoice.SELLER_NM) ?? string.Empty;
            bke.MSTNBAN = Common.NormalizeNullableText(invoice.SELLER_TAX_CD) ?? string.Empty;
            bke.NMUA = Common.NormalizeNullableText(invoice.NMUA_TEN) ?? string.Empty;
            bke.MSTNMUA = Common.NormalizeNullableText(invoice.NMUA_MST);
            bke.DCNMUA = Common.NormalizeNullableText(invoice.NMUA_DCHI);

            var reasons = bke.REASONS.Where(x => x.ISDEL != 1 && HasText(x.LDO)).ToList();
            if (reasons.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BKE_LDO");

            for (var i = 0; i < reasons.Count; i++)
            {
                reasons[i].SORT_ORDER = i + 1;
                reasons[i].LDO = Common.NormalizeNullableText(reasons[i].LDO) ?? string.Empty;
            }

            bke.REASONS = reasons;

            var details = bke.DETAILS.Where(x => x.ISDEL != 1).ToList();
            if (details.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BKE_DETAIL");

            for (var i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                detail.STT ??= i + 1;
                if (!HasText(detail.KHMSHDON))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("KHMSHDON", detail.STT ?? (i + 1));
                if (detail.REF_INVOICE_ID is null or <= 0)
                    throw new ArgumentException("BKE detail must reference a signed invoice in the system");
            }

            bke.DETAILS = details;
        }

        private async Task ValidateBkeReferencedInvoicesAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (!IsMultiRelatedTchdon(invoice.RELATED?.TCHDON) || invoice.BKE_INFO == null)
                return;

            var refIds = invoice.BKE_INFO.DETAILS
                .Where(x => x.ISDEL != 1 && x.REF_INVOICE_ID is > 0)
                .Select(x => x.REF_INVOICE_ID!.Value)
                .Distinct()
                .ToList();

            if (refIds.Count == 0)
                throw new ArgumentException("BKE detail must reference a signed invoice in the system");

            var expectedBuyerTax = NormalizeTaxCode(invoice.NMUA_MST) ?? string.Empty;
            foreach (var refId in refIds)
            {
                var source = (await _repository.GetHeadersAsync(companyCd, refId, null, null, null)).FirstOrDefault()
                    ?? throw new ArgumentException($"Referenced invoice {refId} was not found for bảng kê");

                if (source.IS_SIGNED != 1)
                    throw new ArgumentException($"Referenced invoice {refId} must be signed before adding to bảng kê");

                var sourceBuyerTax = NormalizeTaxCode(source.NMUA_MST) ?? string.Empty;
                if (!string.Equals(sourceBuyerTax, expectedBuyerTax, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Referenced invoice {refId} buyer tax code must match the current invoice");
            }
        }

        private async Task EnsureBkeNumberAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (!IsMultiRelatedTchdon(invoice.RELATED?.TCHDON))
            {
                return;
            }

            var related = invoice.RELATED!;
            var bke = invoice.BKE_INFO ?? new EInvoiceBkeInfo();
            invoice.BKE_INFO = bke;

            var sbke = Common.NormalizeNullableText(bke.SBKE)
                ?? Common.NormalizeNullableText(related.SBKCLQUAN);
            if (!HasText(sbke))
            {
                var year = (bke.NBKE ?? related.NBKCLQUAN ?? DateTime.Today).Year;
                sbke = await _repository.GetNextBkeNoAsync(companyCd, year);
            }

            bke.SBKE = sbke!;
            related.SBKCLQUAN = sbke;
            bke.NBKE ??= related.NBKCLQUAN ?? DateTime.Today;
            related.NBKCLQUAN = bke.NBKE;
        }

        private async Task PersistBkeAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            EInvoiceInfo invoice)
        {
            var bke = invoice.BKE_INFO!;
            bke.INVOICE_ID = invoiceId;
            bke.COMPANY_CD = companyCd;
            bke.SELLER_ID ??= invoice.SELLER_ID;

            var bkeId = await _repository.SetBkeInfoAsync(session, companyCd, userId, invoiceId, bke);
            bke.BKE_ID = bkeId;
            await _repository.DeleteBkeChildrenAsync(session, bkeId);

            foreach (var reason in bke.REASONS.Where(x => x.ISDEL != 1))
            {
                reason.REASON_ID = 0;
                reason.BKE_ID = bkeId;
                await _repository.SetBkeReasonAsync(session, bkeId, reason);
            }

            foreach (var detail in bke.DETAILS.Where(x => x.ISDEL != 1))
            {
                detail.DETAIL_ID = 0;
                detail.BKE_ID = bkeId;
                await _repository.SetBkeDetailAsync(session, bkeId, detail);
            }
        }

        private static EInvoicePxkInfo BuildPxkInfo(EInvoiceInfo invoice)
        {
            var source = invoice.PXK_INFO;
            var fields = EInvoiceWarehouseHelper.ReadFields(source, invoice.EXTRA_JSON);

            return new EInvoicePxkInfo
            {
                PXK_ID = source?.PXK_ID ?? 0,
                INVOICE_ID = invoice.INVOICE_ID,
                PXK_TYPE = Common.NormalizeNullableText(source?.PXK_TYPE) ?? EInvoiceWarehouseHelper.ResolvePxkType(invoice.KHHDON),
                NBAN_DCHI = Common.NormalizeNullableText(fields.NbanDChi),
                LDDNBO = Common.NormalizeNullableText(fields.LddnBo),
                HDKTSO = Common.NormalizeNullableText(fields.HdktSo),
                HDKTNGAY = NormalizePxkDate(source?.HDKTNGAY, fields.HdktNgay),
                HVTNXHANG = Common.NormalizeNullableText(fields.HvtnxHang),
                TNVCHUYEN = Common.NormalizeNullableText(fields.TnvChuyen),
                HDSO = Common.NormalizeNullableText(fields.HdSo),
                PTVCHUYEN = Common.NormalizeNullableText(fields.PtvChuyen),
                EXTRA_JSON = Common.NormalizeNullableText(source?.EXTRA_JSON),
                ISDEL = 0
            };
        }

        private static DateTime? NormalizePxkDate(DateTime? dateValue, string? textValue)
        {
            if (dateValue.HasValue)
            {
                return dateValue.Value.Date;
            }

            var text = Common.NormalizeNullableText(textValue);
            if (!HasText(text))
            {
                return null;
            }

            return DateTime.TryParse(text, out var parsed) ? parsed.Date : null;
        }

        private static bool HasText(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private async Task<string> BuildRawXmlAsync(string companyCd, EInvoiceInfo invoice)
        {
            EInvoiceWarehouseHelper.ValidateWarehouseInvoice(invoice);
            var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
            var decimalSettings = await _settingRepository.GetDecimalSettingsAsync(companyCd, null, null, null, null, false, seller.XSL_ID);
            var formatter = EInvoiceDecimalFormatter.Create(decimalSettings);
            var rawXml = new EInvoiceXmlBuilder(formatter, invoice.DVTTE, invoice.KHMSHDON).Build(invoice, seller);
            if (!HasText(rawXml))
                throw new InvalidOperationException("Failed to generate e-invoice XML");

            ValidateXml(rawXml, "RAW_XML");
            return rawXml;
        }

        private async Task EnsureBkeReadyForSigningAsync(string companyCd, EInvoiceInfo invoice)
        {
            if (invoice.BKE_INFO == null || invoice.BKE_INFO.BKE_ID <= 0)
            {
                invoice.BKE_INFO = await _repository.GetBkeByInvoiceAsync(companyCd, invoice.INVOICE_ID)
                    ?? invoice.BKE_INFO;
            }

            SyncAndValidateBke(invoice);
            await ValidateBkeReferencedInvoicesAsync(companyCd, invoice);

            if (!HasBkeData(invoice.BKE_INFO))
                throw new InvalidOperationException("Bảng kê 01/BK-ĐCTT is required before signing multi-invoice replacement/adjustment");

            if (!HasText(invoice.BKE_INFO!.SBKE) || !invoice.BKE_INFO.NBKE.HasValue)
                throw EInvoiceValidationMessages.RequiredArgument("RELATED_BANG_KE");

            if (invoice.BKE_INFO.REASONS.Count(x => x.ISDEL != 1 && HasText(x.LDO)) == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BKE_LDO");

            if (invoice.BKE_INFO.DETAILS.Count(x => x.ISDEL != 1) == 0)
                throw EInvoiceValidationMessages.RequiredArgument("BKE_DETAIL");

            // Địa chỉ người bán trên BK: lấy từ seller nếu trống.
            if (!HasText(invoice.BKE_INFO.DCNBAN))
            {
                var seller = await ResolveSellerForInvoiceAsync(companyCd, invoice);
                invoice.BKE_INFO.DCNBAN = Common.NormalizeNullableText(seller.SELLER_ADDRESS);
            }
        }

        private static string BuildRawBkeXml(EInvoiceInfo invoice)
        {
            var bke = invoice.BKE_INFO
                ?? throw new InvalidOperationException("Bảng kê is required");
            var rawXml = EInvoiceBkeXmlBuilder.Build(bke);
            if (!HasText(rawXml))
                throw new InvalidOperationException("Failed to generate bảng kê XML");

            ValidateXml(rawXml, "BKE_RAW_XML");
            return rawXml;
        }

        private static void ValidateSignedXmlRoot(string signedXml, string expectedRootName)
        {
            try
            {
                var document = XDocument.Parse(signedXml, LoadOptions.PreserveWhitespace);
                var root = document.Root
                    ?? throw new InvalidOperationException($"Signed {expectedRootName} XML root element is missing.");
                if (!string.Equals(root.Name.LocalName, expectedRootName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Signed XML root must be {expectedRootName}.");
                }
            }
            catch (System.Xml.XmlException ex)
            {
                throw new InvalidOperationException($"Signed {expectedRootName} XML is invalid.", ex);
            }
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

        private async Task EnsureDeletableAsync(string companyCd, long invoiceId)
        {
            var invoice = (await _repository.GetHeadersAsync(companyCd, invoiceId, null, null, null)).FirstOrDefault()
                ?? throw new KeyNotFoundException("E-invoice not found");

            if (invoice.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice is already signed");

            if (EInvoiceCashRegisterHelper.IsCashRegister(invoice.KHHDON) && HasText(invoice.SHDON))
                throw new InvalidOperationException("Hóa đơn MTT đã phát hành không được xóa.");
        }

        private static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
                throw new UnauthorizedAccessException("Company code not found");
        }
    }
}
