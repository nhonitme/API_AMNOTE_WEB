using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceDeclarationService : IEInvoiceDeclarationService
    {
        private static readonly HashSet<string> ValidDetailTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "CTS", "TCGP", "TCTN", "DVHTPT", "DVDUQTCUU", "TNSDUNG", "DKTH"
        };
        private readonly IEInvoiceDeclarationRepository _repository;
        private readonly IEInvoiceMessageRepository _messageRepository;
        private readonly DapperExecutor _db;
        private readonly ILogger<EInvoiceDeclarationService> _logger;

        public EInvoiceDeclarationService(
            IEInvoiceDeclarationRepository repository,
            IEInvoiceMessageRepository messageRepository,
            DapperExecutor db,
            ILogger<EInvoiceDeclarationService> logger)
        {
            _repository = repository;
            _messageRepository = messageRepository;
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EInvoiceDeclarationDto>> SearchAsync(string companyCd, EInvoiceDeclarationSearchRequest request)
        {
            ValidateCompany(companyCd);
            if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate.Value.Date > request.ToDate.Value.Date)
                throw new ArgumentException("FromDate must be earlier than or equal to ToDate");

            var headers = (await _repository.GetHeadersAsync(
                companyCd,
                request.TkhaiId,
                request.FromDate?.Date,
                request.ToDate?.Date,
                Common.NormalizeNullableText(request.Keyword),
                request.IsSigned)).ToList();

            if (request.IncludeDetails)
            {
                foreach (var header in headers)
                {
                    header.DETAILS = (await _repository.GetDetailsAsync(companyCd, header.TKHAI_ID)).ToList();
                }
            }

            return headers.Select(EInvoiceDeclarationMapper.ToDto).ToList();
        }

        public async Task<EInvoiceDeclarationDto?> GetByIdAsync(string companyCd, long tkhaiId)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            var declaration = await GetEntityByIdAsync(companyCd, tkhaiId);
            return declaration == null ? null : EInvoiceDeclarationMapper.ToDto(declaration);
        }

        public async Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetTransmissionMessagesAsync(string companyCd, long tkhaiId)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            var declaration = await GetEntityByIdAsync(companyCd, tkhaiId)
                ?? throw new KeyNotFoundException("E-invoice declaration not found");
            var mtdiep = Common.NormalizeNullableText(declaration.MTDIEP);
            if (!HasText(mtdiep))
            {
                return Array.Empty<EInvoiceMessageReceiveInfo>();
            }

            return await _messageRepository.GetReceiveMessagesByLookupCodeAsync(companyCd, mtdiep!);
        }

        public async Task<EInvoiceDeclarationDto> CreateAsync(string companyCd, string userId, EInvoiceDeclarationSaveRequest request)
        {
            ValidateCompany(companyCd);
            var declaration = EInvoiceDeclarationMapper.ToEntity(request, companyCd);
            declaration.TKHAI_ID = 0;
            declaration.IS_SIGNED = 0;
            NormalizeAndValidate(declaration);
            ApplyMessageCodes(declaration);
            var id = await PersistAsync(companyCd, userId, declaration);
            _logger.LogInformation("EInvoice declaration created. Company: {CompanyCd}, TkhaiId: {TkhaiId}", companyCd, id);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created e-invoice declaration");
        }

        public async Task<EInvoiceDeclarationDto> UpdateAsync(string companyCd, string userId, long tkhaiId, EInvoiceDeclarationSaveRequest request)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            var existing = await GetEntityByIdAsync(companyCd, tkhaiId)
                ?? throw new KeyNotFoundException("E-invoice declaration not found");
            EnsureEditable(existing);

            var declaration = EInvoiceDeclarationMapper.ToEntity(request, companyCd);
            declaration.TKHAI_ID = tkhaiId;
            declaration.IS_SIGNED = existing.IS_SIGNED;
            NormalizeAndValidate(declaration);
            ApplyMessageCodes(declaration, existing);
            await PersistAsync(companyCd, userId, declaration);
            _logger.LogInformation("EInvoice declaration updated. Company: {CompanyCd}, TkhaiId: {TkhaiId}", companyCd, tkhaiId);
            return await GetByIdAsync(companyCd, tkhaiId)
                ?? throw new InvalidOperationException("Failed to fetch updated e-invoice declaration");
        }

        public async Task<EInvoiceDeclarationSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long tkhaiId)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            var declaration = await GetEntityByIdAsync(companyCd, tkhaiId)
                ?? throw new KeyNotFoundException("E-invoice declaration not found");

            if (declaration.IS_SIGNED == 1)
            {
                return new EInvoiceDeclarationSigningPayloadDto
                {
                    TKHAI_ID = declaration.TKHAI_ID,
                    RAW_XML = string.Empty,
                    IS_SIGNED = true
                };
            }

            EnsureEditable(declaration);
            await EnsureMessageCodesAsync(companyCd, userId, declaration);
            ApplyHeaderDefaults(declaration);
            ApplySigningDates(declaration);

            var rawXml = BuildRawXml(declaration);
            return new EInvoiceDeclarationSigningPayloadDto
            {
                TKHAI_ID = declaration.TKHAI_ID,
                RAW_XML = rawXml,
                IS_SIGNED = false
            };
        }

        public async Task<EInvoiceDeclarationDto> SaveSignatureAsync(string companyCd, string userId, long tkhaiId, EInvoiceDeclarationSignRequest request)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");
            if (request == null)
                throw new ArgumentException("Request body must be provided");

            var signedXml = Common.NormalizeNullableText(request.XML);
            if (!HasText(signedXml))
                throw EInvoiceValidationMessages.RequiredArgument("XML");

            var declaration = await GetEntityByIdAsync(companyCd, tkhaiId)
                ?? throw new KeyNotFoundException("E-invoice declaration not found");
            if (declaration.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice declaration is already signed");

            EnsureEditable(declaration);
            await EnsureMessageCodesAsync(companyCd, userId, declaration);
            ApplyHeaderDefaults(declaration);
            ApplySigningDates(declaration);
            await PersistSigningDatesAsync(companyCd, userId, declaration);

            var mtdiep = declaration.MTDIEP
                ?? throw EInvoiceValidationMessages.RequiredArgument("MTDIEP");
            var packagedMessage = EInvoiceMessageXmlBuilder.PackageSignedDeclaration(signedXml!, declaration, mtdiep);
            await SaveSignedXmlAsync(companyCd, userId, tkhaiId, signedXml!, packagedMessage);
            _logger.LogInformation("EInvoice declaration signed. Company: {CompanyCd}, TkhaiId: {TkhaiId}, CertificateThumbprint: {CertificateThumbprint}", companyCd, tkhaiId, request.CERTIFICATE_THUMBPRINT);

            return await GetByIdAsync(companyCd, tkhaiId)
                ?? throw new InvalidOperationException("Failed to fetch signed e-invoice declaration");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, long tkhaiId)
        {
            ValidateCompany(companyCd);
            if (tkhaiId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            await EnsureDeletableAsync(companyCd, tkhaiId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affected = await _repository.DeleteAsync(session, companyCd, tkhaiId, userId);
                session.Commit();
                _logger.LogInformation("EInvoice declaration deleted. Company: {CompanyCd}, TkhaiId: {TkhaiId}", companyCd, tkhaiId);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> tkhaiIds)
        {
            ValidateCompany(companyCd);
            var ids = tkhaiIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument("TkhaiIds");

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
                _logger.LogInformation("EInvoice declaration batch deleted. Company: {CompanyCd}, Count: {Count}", companyCd, ids.Count);
                return affected;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task<EInvoiceDeclarationInfo?> GetEntityByIdAsync(string companyCd, long tkhaiId)
        {
            var declaration = (await _repository.GetHeadersAsync(companyCd, tkhaiId, null, null, null, null)).FirstOrDefault();
            if (declaration == null)
                return null;

            declaration.DETAILS = (await _repository.GetDetailsAsync(companyCd, tkhaiId)).ToList();
            return declaration;
        }

        private async Task<long> PersistAsync(string companyCd, string userId, EInvoiceDeclarationInfo declaration)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var tkhaiId = await _repository.SetHeaderAsync(session, companyCd, userId, declaration);
                await _repository.DeleteDetailsByDeclarationAsync(session, companyCd, tkhaiId, userId);

                var activeDetails = declaration.DETAILS.Where(x => x.ISDEL != 1).ToList();
                for (var index = 0; index < activeDetails.Count; index++)
                {
                    var detail = activeDetails[index];
                    detail.DETAIL_ID = 0;
                    detail.TKHAI_ID = tkhaiId;
                    detail.COMPANY_CD = companyCd;
                    detail.STT ??= index + 1;
                    await _repository.SetDetailAsync(session, companyCd, userId, tkhaiId, detail);
                }

                session.Commit();
                return tkhaiId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private async Task SaveSignedXmlAsync(string companyCd, string userId, long tkhaiId, string signedXml, EInvoicePackagedMessage packagedMessage)
        {
            ValidateXml(signedXml, "XML");
            ValidateXml(packagedMessage.RequestXml, "PACKAGE_XML");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetSignatureAsync(session, companyCd, userId, tkhaiId, signedXml, 1, null);
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
                EInvoiceMessageTargetTypes.Declaration,
                tkhaiId,
                packagedMessage);
        }

        private const string DefaultMso = "01/ĐKTĐ-HĐĐT";
        private const string DefaultTen = "Tờ khai đăng ký/thay đổi thông tin sử dụng hóa đơn điện tử";

        private static void ApplyHeaderDefaults(EInvoiceDeclarationInfo declaration)
        {
            declaration.PBAN = "2.1.1";
            declaration.MSO = DefaultMso;
            declaration.TEN = DefaultTen;
        }

        private static void NormalizeAndValidate(EInvoiceDeclarationInfo declaration)
        {
            ApplyHeaderDefaults(declaration);
            declaration.IS_SIGNED = declaration.IS_SIGNED == 1 ? 1 : 0;
            declaration.HTHUC = declaration.HTHUC is 1 or 2 ? declaration.HTHUC : 1;
            declaration.CMA = NormalizeFlag(declaration.CMA);
            declaration.CMTMTTIEN = NormalizeFlag(declaration.CMTMTTIEN);
            declaration.KCMTMTTIEN = NormalizeFlag(declaration.KCMTMTTIEN);
            declaration.KCMA = NormalizeFlag(declaration.KCMA);
            declaration.NNTDBKKHAN = NormalizeFlag(declaration.NNTDBKKHAN);
            declaration.NNTKTDNUBND = 0;
            declaration.CQXLTSCONG = NormalizeFlag(declaration.CQXLTSCONG);
            declaration.CDLTTDCQT = NormalizeFlag(declaration.CDLTTDCQT);
            declaration.CDLQTCTN = NormalizeFlag(declaration.CDLQTCTN);
            declaration.TCNNGOAI = NormalizeFlag(declaration.TCNNGOAI);
            declaration.CDDU = NormalizeFlag(declaration.CDDU);
            declaration.CDLTHDTHU = NormalizeFlag(declaration.CDLTHDTHU);
            declaration.CBTHOP = NormalizeFlag(declaration.CBTHOP);
            declaration.CTTCTGDICH = NormalizeFlag(declaration.CTTCTGDICH);
            declaration.HDGTGT = NormalizeFlag(declaration.HDGTGT);
            declaration.HDGTGTTHBLAI = NormalizeFlag(declaration.HDGTGTTHBLAI);
            declaration.HDBHANG = NormalizeFlag(declaration.HDBHANG);
            declaration.HDBHTHBLAI = NormalizeFlag(declaration.HDBHTHBLAI);
            declaration.HDTMAI = NormalizeFlag(declaration.HDTMAI);
            declaration.HDNCCNNGOAI = NormalizeFlag(declaration.HDNCCNNGOAI);
            declaration.HDBTSCONG = NormalizeFlag(declaration.HDBTSCONG);
            declaration.HDBHDTQGIA = NormalizeFlag(declaration.HDBHDTQGIA);
            declaration.HDKHAC = NormalizeFlag(declaration.HDKHAC);
            declaration.CTU = NormalizeFlag(declaration.CTU);
            declaration.GTINH = null;
            declaration.MQTNDDPLUAT = NormalizeNationalityCode(declaration.MQTNDDPLUAT, declaration.QTICH);

            if (!declaration.NLAP.HasValue)
                declaration.NLAP = DateTime.Today;
            else
                declaration.NLAP = declaration.NLAP.Value.Date;
            if (!HasText(declaration.TNNT))
                throw EInvoiceValidationMessages.RequiredArgument("TNNT");
            if (!HasText(declaration.MST))
                throw EInvoiceValidationMessages.RequiredArgument("MST");
            if (declaration.IS_SIGNED == 1)
                throw new ArgumentException("Signed declaration cannot be saved");

            var activeDetails = declaration.DETAILS.Where(x => x.ISDEL != 1).ToList();
            for (var index = 0; index < activeDetails.Count; index++)
            {
                var detail = activeDetails[index];
                detail.DETAIL_TYPE = NormalizeDetailType(detail.DETAIL_TYPE);
                detail.STT ??= index + 1;
                ValidateDetail(detail);
            }
        }

        private static void ValidateDetail(EInvoiceDeclarationDetail detail)
        {
            if (detail.DETAIL_TYPE == "CTS")
            {
                if (!HasText(detail.TTCHUC))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TTCHUC", detail.STT ?? 0);
                if (!HasText(detail.SERI))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("SERI", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "TCGP")
            {
                if (!HasText(detail.TTCGP))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TTCGP", detail.STT ?? 0);
                if (!HasText(detail.MSTTCGP))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTTCGP", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "TCTN")
            {
                if (!HasText(detail.TTCTN))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TTCTN", detail.STT ?? 0);
                if (!HasText(detail.MSTTCTN))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTTCTN", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "DVHTPT")
            {
                if (!HasText(detail.TDVHTPT))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TDVHTPT", detail.STT ?? 0);
                if (!HasText(detail.MSTDVHTPT))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTDVHTPT", detail.STT ?? 0);
                if (!detail.TNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TNGAY", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "DVDUQTCUU")
            {
                if (!HasText(detail.TDVI))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TDVI", detail.STT ?? 0);
                if (!HasText(detail.MSTDUQ))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTDUQ", detail.STT ?? 0);
                if (detail.HDBRMVAO is not (1 or 2 or 3))
                    throw new ArgumentException($"HDBRMVAO is invalid at line {detail.STT ?? 0}");
                if (!detail.TDLHDTNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TDLHDTNGAY", detail.STT ?? 0);
                if (!detail.TDLHDDNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TDLHDDNGAY", detail.STT ?? 0);
                if (!detail.TGUQTNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TGUQTNGAY", detail.STT ?? 0);
                if (!detail.TGUQDNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TGUQDNGAY", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "TNSDUNG")
            {
                if (!detail.TNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TNGAY", detail.STT ?? 0);
                if (!detail.DNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("DNGAY", detail.STT ?? 0);
                if (!HasText(detail.TTCGP))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TTCGP", detail.STT ?? 0);
                if (!HasText(detail.MSTTCGP))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTTCGP", detail.STT ?? 0);
            }
            else if (detail.DETAIL_TYPE == "DKTH")
            {
                if (!HasText(detail.TLHDON))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TLHDON", detail.STT ?? 0);
                if (!detail.KHMSHDON.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("KHMSHDON", detail.STT ?? 0);
                if (!HasText(detail.KHHDON))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("KHHDON", detail.STT ?? 0);
                if (!HasText(detail.TENDKTH))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TENDKTH", detail.STT ?? 0);
                if (!HasText(detail.MSTDKTH))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MSTDKTH", detail.STT ?? 0);
                if (!HasText(detail.MDICH))
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("MDICH", detail.STT ?? 0);
                if (!detail.TNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("TNGAY", detail.STT ?? 0);
                if (!detail.DNGAY.HasValue)
                    throw EInvoiceValidationMessages.RequiredAtLineArgument("DNGAY", detail.STT ?? 0);
            }
        }

        private async Task EnsureMessageCodesAsync(string companyCd, string userId, EInvoiceDeclarationInfo declaration, EInvoiceDeclarationInfo? existing = null)
        {
            var hadMtdiep = HasText(declaration.MTDIEP) || HasText(existing?.MTDIEP);
            ApplyMessageCodes(declaration, existing);

            if (hadMtdiep)
                return;

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetHeaderAsync(session, companyCd, userId, declaration);
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void ApplyMessageCodes(EInvoiceDeclarationInfo declaration, EInvoiceDeclarationInfo? existing = null)
        {
            declaration.MTDIEP = Common.NormalizeNullableText(declaration.MTDIEP)
                ?? Common.NormalizeNullableText(existing?.MTDIEP);

            if (!HasText(declaration.MTDIEP))
                declaration.MTDIEP = EInvoiceMessageXmlBuilder.GenerateMessageCode();

            declaration.MGDDTU = Common.NormalizeNullableText(declaration.MGDDTU)
                ?? Common.NormalizeNullableText(existing?.MGDDTU);
        }

        private async Task PersistSigningDatesAsync(string companyCd, string userId, EInvoiceDeclarationInfo declaration)
        {
            if (declaration.TKHAI_ID <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("TKHAI_ID");

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await _repository.SetHeaderAsync(session, companyCd, userId, declaration);

                foreach (var detail in declaration.DETAILS.Where(x => x.ISDEL != 1 && x.DETAIL_TYPE is "TCGP" or "TCTN"))
                {
                    await _repository.SetDetailAsync(session, companyCd, userId, declaration.TKHAI_ID, detail);
                }

                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static void ApplySigningDates(EInvoiceDeclarationInfo declaration)
        {
            var today = DateTime.Today;
            declaration.NLAP = today;

            foreach (var detail in declaration.DETAILS.Where(x => x.ISDEL != 1))
            {
                if (detail.DETAIL_TYPE == "TCGP")
                {
                    detail.TNGAY = today;
                    detail.DNGAY = today;
                }
                else if (detail.DETAIL_TYPE == "TCTN")
                {
                    detail.TNGAY = today;
                    detail.DNGAY = null;
                }
            }
        }

        private static string BuildRawXml(EInvoiceDeclarationInfo declaration)
        {
            var rawXml = EInvoiceDeclarationXmlBuilder.Build(declaration);
            if (!HasText(rawXml))
                throw new InvalidOperationException("Failed to generate e-invoice declaration XML");

            ValidateXml(rawXml, "XML");
            return rawXml;
        }

        private static string NormalizeDetailType(string? detailType)
        {
            var normalized = Common.NormalizeNullableText(detailType)?.ToUpperInvariant();
            if (!HasText(normalized) || !ValidDetailTypes.Contains(normalized!))
                throw new ArgumentException("DETAIL_TYPE is invalid");
            return normalized!;
        }

        private static string NormalizeNationalityCode(string? code, string? nationalityName)
        {
            var normalizedCode = Common.NormalizeNullableText(code)?.ToUpperInvariant();
            if (HasText(normalizedCode))
                return normalizedCode!.Length > 2 ? normalizedCode[..2] : normalizedCode;

            var name = Common.NormalizeNullableText(nationalityName) ?? string.Empty;
            var compact = new string(name
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .ToArray())
                .Replace("đ", "d", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();

            if (compact is "VN" or "VIETNAM")
                return "VN";

            return "VN";
        }

        private static int NormalizeFlag(int value)
        {
            return value == 1 ? 1 : 0;
        }

        private static void EnsureEditable(EInvoiceDeclarationInfo declaration)
        {
            if (declaration.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice declaration is already signed");
        }

        private async Task EnsureDeletableAsync(string companyCd, long tkhaiId)
        {
            var declaration = (await _repository.GetHeadersAsync(companyCd, tkhaiId, null, null, null, null)).FirstOrDefault()
                ?? throw new KeyNotFoundException("E-invoice declaration not found");

            if (declaration.IS_SIGNED == 1)
                throw new InvalidOperationException("E-invoice declaration is already signed");
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
