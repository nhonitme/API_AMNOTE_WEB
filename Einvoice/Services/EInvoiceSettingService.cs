using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceSettingService : IEInvoiceSettingService
    {
        private static readonly HashSet<string> ApplyTargets = new(StringComparer.OrdinalIgnoreCase) { "TAX_XML", "INTERNAL_REPORT", "UI" };
        private static readonly HashSet<string> FieldScopes = new(StringComparer.OrdinalIgnoreCase) { "HEADER", "DETAIL" };
        private static readonly HashSet<string> CurrencyScopes = new(StringComparer.OrdinalIgnoreCase) { "ANY", "VND", "FC" };
        private static readonly HashSet<string> RoundModes = new(StringComparer.OrdinalIgnoreCase) { "ROUND", "TRUNCATE", "CEIL", "FLOOR" };
        private static readonly HashSet<string> ValueTypes = new(StringComparer.OrdinalIgnoreCase) { "STRING", "NUMBER", "BOOLEAN", "JSON" };

        private readonly IEInvoiceSettingRepository _repository;
        private readonly IEInvoiceSellerXslTemplateRepository _xslTemplateRepository;
        private readonly IEInvoiceSellerRepository _sellerRepository;
        private readonly DapperExecutor _db;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EInvoiceSettingService> _logger;

        public EInvoiceSettingService(
            IEInvoiceSettingRepository repository,
            IEInvoiceSellerXslTemplateRepository xslTemplateRepository,
            IEInvoiceSellerRepository sellerRepository,
            DapperExecutor db,
            IWebHostEnvironment environment,
            ILogger<EInvoiceSettingService> logger)
        {
            _repository = repository;
            _xslTemplateRepository = xslTemplateRepository;
            _sellerRepository = sellerRepository;
            _db = db;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IReadOnlyList<EInvoiceDecimalSettingDto>> SearchDecimalSettingsAsync(string companyCd, EInvoiceDecimalSettingSearchRequest request)
        {
            var rows = await _repository.GetDecimalSettingsAsync(
                companyCd,
                request.SettingId,
                NormalizeNullableUpper(request.ApplyTarget),
                NormalizeNullableUpper(request.FieldScope),
                request.Keyword,
                request.IncludeInactive,
                request.XslId);
            return rows.Select(EInvoiceSettingMapper.ToDecimalDto).ToList();
        }

        public async Task<EInvoiceDecimalSettingDto?> GetDecimalSettingByIdAsync(string companyCd, long settingId)
        {
            var rows = await _repository.GetDecimalSettingsAsync(companyCd, settingId, null, null, null, true);
            var entity = rows.FirstOrDefault();
            return entity == null ? null : EInvoiceSettingMapper.ToDecimalDto(entity);
        }

        public async Task<EInvoiceDecimalSettingDto> SaveDecimalSettingAsync(string companyCd, string userId, EInvoiceDecimalSettingSaveRequest request)
        {
            var entity = NormalizeDecimalSetting(EInvoiceSettingMapper.ToDecimalEntity(request, companyCd));

            if (entity.SETTING_ID > 0)
            {
                var existing = await GetDecimalSettingByIdAsync(companyCd, entity.SETTING_ID);
                if (existing == null)
                    throw new ArgumentException("E-invoice decimal setting not found");

                if (existing.XSL_ID != entity.XSL_ID)
                    throw new ArgumentException("XSL_ID must match the existing decimal setting scope");
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var settingId = await _repository.SetDecimalSettingAsync(session, companyCd, userId, entity);
                session.Commit();

                await _repository.ClearDecimalSettingsCacheAsync(companyCd);

                _logger.LogInformation("E-invoice decimal setting saved: settingId={SettingId}, company={CompanyCd}", settingId, companyCd);
                return await GetDecimalSettingByIdAsync(companyCd, settingId)
                    ?? throw new InvalidOperationException("E-invoice decimal setting was saved but could not be reloaded");
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<EInvoiceUserSettingDto>> SearchUserSettingsAsync(string companyCd, EInvoiceUserSettingSearchRequest request)
        {
            var rows = await _repository.GetUserSettingsAsync(
                companyCd,
                request.SettingId,
                Common.NormalizeNullableText(request.UserId),
                request.Keyword,
                request.IncludeDeleted);
            return rows.Select(EInvoiceSettingMapper.ToUserSettingDto).ToList();
        }

        public async Task<EInvoiceUserSettingDto?> GetUserSettingByIdAsync(string companyCd, long settingId)
        {
            var rows = await _repository.GetUserSettingsAsync(companyCd, settingId, null, null, true);
            var entity = rows.FirstOrDefault();
            return entity == null ? null : EInvoiceSettingMapper.ToUserSettingDto(entity);
        }

        public async Task<EInvoiceUserSettingDto> SaveUserSettingAsync(string companyCd, string userId, EInvoiceUserSettingSaveRequest request)
        {
            var entity = await ResolveUserSettingIdentityAsync(companyCd, NormalizeUserSetting(EInvoiceSettingMapper.ToUserSettingEntity(request), companyCd));

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var settingId = await _repository.SetUserSettingAsync(session, userId, entity);
                session.Commit();

                await _repository.ClearUserSettingsCacheAsync(companyCd);

                _logger.LogInformation("E-invoice user setting saved: settingId={SettingId}, company={CompanyCd}", settingId, companyCd);
                return await GetUserSettingByIdAsync(companyCd, settingId)
                    ?? throw new InvalidOperationException("E-invoice user setting was saved but could not be reloaded");
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<EInvoiceAdminSettingDto>> SearchAdminSettingsAsync(string companyCd, EInvoiceAdminSettingSearchRequest request)
        {
            var rows = await _repository.GetAdminSettingsAsync(
                companyCd,
                request.SettingId,
                NormalizeNullableUpper(request.SettingType),
                request.Keyword,
                request.IncludeDeleted);
            return rows.Select(EInvoiceSettingMapper.ToAdminSettingDto).ToList();
        }

        public async Task<EInvoiceAdminSettingDto?> GetAdminSettingByIdAsync(string companyCd, long settingId)
        {
            var rows = await _repository.GetAdminSettingsAsync(companyCd, settingId, null, null, true);
            var entity = rows.FirstOrDefault();
            return entity == null ? null : EInvoiceSettingMapper.ToAdminSettingDto(entity);
        }

        public Task<IReadOnlyList<EInvoiceTemplateDesignerDesign>> GetTemplateDesignerDesignsAsync(string companyCd, long xslId)
            => _xslTemplateRepository.GetDesignsAsync(companyCd, xslId);

        public Task<EInvoiceTemplateDesignerDesign> CloneTemplateDesignerAsync(string companyCd, string userId, long xslId, long designId)
            => _xslTemplateRepository.CloneDesignAsync(companyCd, userId, xslId, designId);

        public async Task<EInvoiceTemplateDesignerDraft> SaveTemplateDesignerDraftAsync(
            string companyCd,
            string userId,
            long xslId,
            EInvoiceTemplateDesignerDraftSaveRequest request)
        {
            var rawXsl = request.XSL_CONTENT!;
            var xsl = EInvoicePrintImageHelper.ClearXslImageParams(rawXsl);
            var existing = await _xslTemplateRepository.GetDesignAsync(companyCd, xslId, request.DESIGN_ID);
            var logoPath = EInvoiceFtpClient.ResolveDraftImagePath(
                "logo",
                companyCd,
                request.LOGO_PATH,
                EInvoicePrintImageHelper.ReadXslImageParam(rawXsl, "logoImage"),
                existing?.LOGO_PATH);
            var backgroundPath = EInvoiceFtpClient.ResolveDraftImagePath(
                "background",
                companyCd,
                request.BACKGROUND_PATH,
                EInvoicePrintImageHelper.ReadXslImageParam(rawXsl, "backgroundImage"),
                existing?.BACKGROUND_PATH);
            var nenPath = EInvoiceFtpClient.ResolveDraftImagePath(
                "invoice-background",
                companyCd,
                request.INVOICE_BACKGROUND_PATH,
                EInvoicePrintImageHelper.ReadXslImageParam(rawXsl, "nenImage"),
                existing?.INVOICE_BACKGROUND_PATH);
            var vienPath = EInvoiceFtpClient.ResolveDraftImagePath(
                "border",
                companyCd,
                request.INVOICE_BORDER_PATH,
                EInvoicePrintImageHelper.ReadXslImageParam(rawXsl, "vienHdImage"),
                existing?.INVOICE_BORDER_PATH);

            return await _xslTemplateRepository.SaveDraftAsync(
                companyCd,
                userId,
                xslId,
                request.DESIGN_ID,
                xsl,
                logoPath,
                nenPath,
                vienPath,
                backgroundPath);
        }

        public async Task<EInvoiceSellerXslTemplateContentResult> PublishTemplateDesignerAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId)
        {
            try
            {
                var data = await _xslTemplateRepository.PublishDesignAsync(companyCd, userId, xslId, designId);
                await _sellerRepository.ClearSellersCacheAsync(companyCd);
                return data;
            }
            catch (Exception ex) when (TryGetMysqlSignalMessage(ex, out var signalMessage))
            {
                if (signalMessage.Contains("Không tìm thấy mẫu hóa đơn", StringComparison.OrdinalIgnoreCase)
                    || signalMessage.Contains("Invoice template not found", StringComparison.OrdinalIgnoreCase))
                {
                    throw new KeyNotFoundException(signalMessage);
                }

                throw new InvalidOperationException(signalMessage);
            }
        }

        public async Task<EInvoiceDesignerImageResult> UploadTemplateDesignerImageAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string imageKind,
            string originalFileName,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            if (bytes.Length == 0)
                throw EInvoiceValidationMessages.RequiredArgument("file");
            if (bytes.Length > 10 * 1024 * 1024)
                throw new ArgumentException("Image file must not exceed 10 MB");
            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");
            if (!EInvoiceFtpClient.TryNormalizeImageKind(imageKind, out var normalizedKind))
                throw new ArgumentException("Unsupported image kind");

            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"))
                throw new ArgumentException("Only PNG, JPG, GIF and WEBP image files are supported");

            if (await _xslTemplateRepository.GetDesignAsync(companyCd, xslId, designId) == null)
                throw new KeyNotFoundException("Không tìm thấy bản thiết kế");

            if (!EInvoiceFtpClient.TrySanitizeCompanySegment(companyCd, out var company))
                throw EInvoiceValidationMessages.RequiredArgument("COMPANY_CD");
            var remoteDirectory = EInvoiceFtpClient.ResolveCompanyImageRemoteDirectory(normalizedKind, companyCd);
            var remotePath = $"{remoteDirectory}/{company}_{xslId}_d{designId}_{normalizedKind}_{Guid.NewGuid():N}{extension}";
            await Task.Run(() =>
            {
                EInvoiceFtpClient.EnsureRemoteDirectoryExists(remoteDirectory);
                EInvoiceFtpClient.UploadBytes(remotePath, bytes);
            }, cancellationToken);
            EInvoicePrintImageHelper.SaveCachedImage(remotePath, bytes, _environment);
            await _xslTemplateRepository.SetDesignImageAsync(companyCd, userId, xslId, designId, normalizedKind, remotePath);

            return new EInvoiceDesignerImageResult
            {
                PATH = remotePath,
                FILE_NAME = Path.GetFileName(remotePath),
                IMAGE_KIND = normalizedKind,
                DESIGN_ID = designId
            };
        }

        public async Task<EInvoiceDesignerImageResult> SelectTemplateDesignerImageAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string imageKind,
            string? fileName,
            string? path = null)
        {
            if (!EInvoiceFtpClient.TryNormalizeImageKind(imageKind, out var normalizedKind))
                throw new ArgumentException("Unsupported image kind");
            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");
            if (!EInvoiceFtpClient.TryResolveAccessibleImagePath(normalizedKind, companyCd, fileName, path, out var remotePath))
                throw EInvoiceValidationMessages.RequiredArgument("FILE_NAME");
            if (!EInvoiceFtpClient.RemoteFileExists(remotePath))
                throw new KeyNotFoundException("Image file not found");
            if (await _xslTemplateRepository.GetDesignAsync(companyCd, xslId, designId) == null)
                throw new KeyNotFoundException("Không tìm thấy bản thiết kế");

            await _xslTemplateRepository.SetDesignImageAsync(companyCd, userId, xslId, designId, normalizedKind, remotePath);
            return new EInvoiceDesignerImageResult
            {
                PATH = remotePath,
                FILE_NAME = Path.GetFileName(remotePath),
                IMAGE_KIND = normalizedKind,
                DESIGN_ID = designId
            };
        }

        public async Task<IReadOnlyList<EInvoiceFtpImageFile>> ListTemplateDesignerImagesAsync(
            string companyCd,
            string imageKind,
            CancellationToken cancellationToken = default)
        {
            if (!EInvoiceFtpClient.TryNormalizeImageKind(imageKind, out var normalizedKind))
                throw new ArgumentException("Unsupported image kind");
            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");

            try
            {
                return await Task.Run(() => EInvoiceFtpClient.ListImageFiles(normalizedKind, companyCd), cancellationToken);
            }
            catch
            {
                throw new InvalidOperationException("Cannot list images from FTP");
            }
        }

        public async Task<(byte[] Bytes, string ContentType)> GetTemplateDesignerImageFileAsync(
            string companyCd,
            string imageKind,
            string? fileName,
            string? path,
            CancellationToken cancellationToken = default)
        {
            if (!EInvoiceFtpClient.TryNormalizeImageKind(imageKind, out var normalizedKind))
                throw new ArgumentException("Unsupported image kind");
            if (!EInvoiceFtpClient.TryResolveAccessibleImagePath(normalizedKind, companyCd, fileName, path, out var remotePath))
                throw EInvoiceValidationMessages.RequiredArgument("fileName");

            var cached = EInvoicePrintImageHelper.TryReadCachedBytes(remotePath, _environment);
            if (cached is { Length: > 0 })
            {
                return (cached, EInvoiceFtpClient.GetImageContentType(remotePath));
            }

            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");

            try
            {
                var bytes = await Task.Run(() => EInvoiceFtpClient.DownloadBytes(remotePath), cancellationToken);
                if (bytes.Length >= 32)
                {
                    EInvoicePrintImageHelper.SaveCachedImage(remotePath, bytes, _environment);
                }

                return (bytes, EInvoiceFtpClient.GetImageContentType(remotePath));
            }
            catch (Exception ex)
            {
                throw new KeyNotFoundException($"Image file not found: {ex.Message}");
            }
        }

        public async Task<IReadOnlyList<EInvoiceFtpXslFile>> ListTemplateDesignerXslSamplesAsync(
            CancellationToken cancellationToken = default)
        {
            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");

            try
            {
                return await Task.Run(() => EInvoiceFtpClient.ListDefaultXslFiles(), cancellationToken);
            }
            catch
            {
                throw new InvalidOperationException("Cannot list XSL samples from FTP");
            }
        }

        public async Task<EInvoiceFtpXslContentResult> GetTemplateDesignerXslSampleFileAsync(
            string? fileName,
            CancellationToken cancellationToken = default)
        {
            if (!EInvoiceFtpClient.TryBuildDefaultXslPath(fileName, out var remotePath))
                throw EInvoiceValidationMessages.RequiredArgument("fileName");
            if (!EInvoiceFtpClient.IsConfigured())
                throw new InvalidOperationException("E-invoice FTP is not configured");

            try
            {
                var content = await Task.Run(() => EInvoiceFtpClient.DownloadText(remotePath), cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                    throw new KeyNotFoundException("XSL sample is empty");
                if (!EInvoiceFtpClient.LooksLikeXmlOrXsl(content, out var reason))
                    throw new InvalidOperationException(reason ?? "Downloaded file is not a valid XSL");

                return new EInvoiceFtpXslContentResult
                {
                    FILE_NAME = Path.GetFileName(remotePath),
                    PATH = remotePath,
                    XSL_CONTENT = content
                };
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new KeyNotFoundException($"XSL sample not found: {ex.Message}");
            }
        }

        public Task<EInvoiceSellerXslTemplateMetaResult> CreateSellerXslTemplateAsync(
            string companyCd,
            string userId,
            EInvoiceSellerXslTemplateMetaSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            request.XSL_ID = 0;
            return SaveSellerXslTemplateMetaAsync(companyCd, userId, request, cancellationToken);
        }

        public Task<EInvoiceSellerXslTemplateMetaResult> UpdateSellerXslTemplateAsync(
            string companyCd,
            string userId,
            long xslId,
            EInvoiceSellerXslTemplateMetaSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            request.XSL_ID = xslId;
            return SaveSellerXslTemplateMetaAsync(companyCd, userId, request, cancellationToken);
        }

        public async Task<int> DeleteSellerXslTemplatesAsync(string companyCd, string userId, IEnumerable<long> xslIds)
        {
            var deleted = 0;
            foreach (var xslId in xslIds.Where(id => id > 0).Distinct())
            {
                try
                {
                    var affected = await _xslTemplateRepository.DeleteAsync(companyCd, userId, xslId);
                    deleted += affected > 0 ? 1 : 0;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
            }

            await _sellerRepository.ClearSellersCacheAsync(companyCd);
            return deleted;
        }

        public async Task<EInvoiceSellerDto> UpdateSellerInfoAsync(
            string companyCd,
            string userId,
            long sellerId,
            EInvoiceSellerInfoSaveRequest request)
        {
            var updated = await _sellerRepository.UpdateSellerInfoAsync(companyCd, userId, sellerId, request);
            if (updated == null)
                throw new KeyNotFoundException("E-invoice seller not found");

            return EInvoiceMapper.ToSellerDto(updated);
        }

        private async Task<EInvoiceSellerXslTemplateMetaResult> SaveSellerXslTemplateMetaAsync(
            string companyCd,
            string userId,
            EInvoiceSellerXslTemplateMetaSaveRequest request,
            CancellationToken cancellationToken)
        {
            var created = request.XSL_ID <= 0;
            var khms = EInvoiceSellerThdonResolver.NormalizeKhmsHdon(request.KHMSHDON);
            var khhd = EInvoiceSellerThdonResolver.NormalizeKhhdon(request.KHHDON);
            if (string.IsNullOrWhiteSpace(khms))
                throw EInvoiceValidationMessages.RequiredArgument("KHMSHDON");
            if (string.IsNullOrWhiteSpace(khhd))
                throw EInvoiceValidationMessages.RequiredArgument("KHHDON");

            string? xslContent = null;
            if (created || !string.IsNullOrWhiteSpace(request.XSL_FILE_NAME) || !string.IsNullOrWhiteSpace(request.XSL_CONTENT))
            {
                xslContent = Common.NormalizeNullableText(request.XSL_CONTENT);
                if (string.IsNullOrWhiteSpace(xslContent))
                {
                    if (!EInvoiceFtpClient.TryBuildDefaultXslPath(request.XSL_FILE_NAME, out var remotePath))
                        throw EInvoiceValidationMessages.RequiredArgument("XSL_FILE_NAME");
                    if (!EInvoiceFtpClient.IsConfigured())
                        throw new InvalidOperationException("E-invoice FTP is not configured");
                    try
                    {
                        xslContent = await Task.Run(() => EInvoiceFtpClient.DownloadText(remotePath), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Cannot download XSL sample: {ex.Message}", ex);
                    }
                }

                if (string.IsNullOrWhiteSpace(xslContent))
                    throw EInvoiceValidationMessages.RequiredArgument("XSL_CONTENT");
                if (!EInvoiceFtpClient.LooksLikeXmlOrXsl(xslContent, out var xslReason))
                    throw new InvalidOperationException(xslReason ?? "Invalid XSL content");
            }

            var thdon = Common.NormalizeNullableText(request.THDON)
                ?? await EInvoiceSellerThdonResolver.ResolveAsync(khms, khhd);
            var templateNm = Common.NormalizeNullableText(request.TEMPLATE_NM);
            if (string.IsNullOrWhiteSpace(templateNm) && !string.IsNullOrWhiteSpace(request.XSL_FILE_NAME))
            {
                templateNm = Path.GetFileNameWithoutExtension(request.XSL_FILE_NAME);
            }

            try
            {
                var data = await _xslTemplateRepository.SaveMetaAsync(
                    companyCd,
                    userId,
                    request,
                    templateNm,
                    thdon,
                    khms!,
                    khhd!,
                    xslContent);
                await _sellerRepository.ClearSellersCacheAsync(companyCd);
                return data;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message, ex);
            }
        }

        private static bool TryGetMysqlSignalMessage(Exception ex, out string message)
        {
            message = string.Empty;
            for (var current = ex; current != null; current = current.InnerException)
            {
                var text = current.Message?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("đã phát hành", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Invoice template", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Design with XSL_CONTENT", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Template IS_ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    message = text;
                    return true;
                }
            }

            return false;
        }

        private async Task<EInvoiceUserSetting> ResolveUserSettingIdentityAsync(string companyCd, EInvoiceUserSetting setting)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var targetCompanyCd = Common.NormalizeNullableText(setting.COMPANY_CD) ?? string.Empty;
            var targetUserId = Common.NormalizeNullableText(setting.USER_ID) ?? string.Empty;
            var targetSettingKey = Common.NormalizeNullableText(setting.SETTING_KEY) ?? string.Empty;

            if (targetSettingKey.Length == 0)
            {
                return setting;
            }

            if (!string.Equals(targetCompanyCd, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase))
            {
                if (setting.SETTING_ID > 0)
                {
                    var existingById = await GetUserSettingByIdAsync(normalizedCompanyCd, setting.SETTING_ID);
                    if (existingById != null &&
                        !string.Equals(existingById.COMPANY_CD, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase))
                    {
                        setting.SETTING_ID = 0;
                    }
                }

                targetCompanyCd = normalizedCompanyCd;
                setting.COMPANY_CD = normalizedCompanyCd;
            }

            var scopedRow = await FindUserSettingRowAsync(normalizedCompanyCd, targetUserId, targetSettingKey, targetCompanyCd);
            if (scopedRow != null)
            {
                setting.SETTING_ID = scopedRow.SETTING_ID;
                setting.COMPANY_CD = scopedRow.COMPANY_CD;
                setting.USER_ID = scopedRow.USER_ID;
                setting.ISDEL = 0;
                return setting;
            }

            if (setting.SETTING_ID > 0)
            {
                var existingById = await GetUserSettingByIdAsync(normalizedCompanyCd, setting.SETTING_ID);
                if (existingById == null ||
                    !string.Equals(existingById.COMPANY_CD, targetCompanyCd, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existingById.USER_ID, targetUserId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(existingById.SETTING_KEY, targetSettingKey, StringComparison.OrdinalIgnoreCase))
                {
                    setting.SETTING_ID = 0;
                }
            }

            return setting;
        }

        private async Task<EInvoiceUserSetting?> FindUserSettingRowAsync(
            string companyCd,
            string userId,
            string settingKey,
            string? matchCompanyCd = null)
        {
            var rows = await _repository.GetUserSettingsAsync(companyCd, null, userId, null, true);
            return rows.FirstOrDefault(row =>
                string.Equals(row.SETTING_KEY, settingKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.USER_ID, userId, StringComparison.OrdinalIgnoreCase) &&
                (matchCompanyCd == null ||
                 string.Equals(row.COMPANY_CD, matchCompanyCd, StringComparison.OrdinalIgnoreCase)));
        }

        private static EInvoiceUserSetting NormalizeUserSetting(EInvoiceUserSetting setting, string companyCd)
        {
            setting.COMPANY_CD = Common.NormalizeNullableText(setting.COMPANY_CD) ?? string.Empty;
            setting.USER_ID = Common.NormalizeNullableText(setting.USER_ID) ?? string.Empty;
            setting.SETTING_KEY = NormalizeRequired(setting.SETTING_KEY, "SETTING_KEY", 100);
            setting.SETTING_VALUE = NormalizeOptional(setting.SETTING_VALUE, 1000);
            setting.VALUE_TYPE = NormalizeEnum(setting.VALUE_TYPE, "VALUE_TYPE", ValueTypes, 20);
            setting.ISDEL = 0;

            if (setting.SETTING_ID <= 0 && string.IsNullOrWhiteSpace(setting.COMPANY_CD))
            {
                setting.COMPANY_CD = companyCd;
            }

            return setting;
        }

        private static EInvoiceDecimalSetting NormalizeDecimalSetting(EInvoiceDecimalSetting setting)
        {
            setting.APPLY_TARGET = NormalizeEnum(setting.APPLY_TARGET, "APPLY_TARGET", ApplyTargets, 30);
            setting.XSL_ID = setting.XSL_ID > 0 ? setting.XSL_ID : 0;
            setting.FIELD_SCOPE = NormalizeEnum(setting.FIELD_SCOPE, "FIELD_SCOPE", FieldScopes, 20);
            setting.FIELD_NAME = NormalizeRequired(setting.FIELD_NAME, "FIELD_NAME", 50).ToUpperInvariant();
            setting.LABEL_TEXT = NormalizeOptional(setting.LABEL_TEXT, 100);
            setting.CAPTION = NormalizeOptional(setting.CAPTION, 255);
            setting.CURRENCY_SCOPE = NormalizeEnum(setting.CURRENCY_SCOPE, "CURRENCY_SCOPE", CurrencyScopes, 20);
            setting.ROUND_MODE = NormalizeEnum(setting.ROUND_MODE, "ROUND_MODE", RoundModes, 20);
            setting.DECIMAL_SCALE = Math.Clamp(setting.DECIMAL_SCALE, 0, 12);
            setting.IS_ACTIVE = NormalizeFlag(setting.IS_ACTIVE, 1);
            setting.NOTE = NormalizeOptional(setting.NOTE, 500) ?? string.Empty;
            return setting;
        }

        private static List<long> NormalizeIds(IEnumerable<long> ids, string fieldName)
        {
            var normalized = ids.Where(id => id > 0).Distinct().ToList();
            if (normalized.Count == 0)
                throw EInvoiceValidationMessages.RequiredArgument(fieldName);

            return normalized;
        }

        private static string NormalizeRequired(string? value, string fieldName, int maxLength)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
                throw EInvoiceValidationMessages.RequiredArgument(fieldName);

            if (normalized.Length > maxLength)
                throw new ArgumentException($"{fieldName} exceeds {maxLength} characters");

            return normalized;
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
                return null;

            return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
        }

        private static string? NormalizeNullableUpper(string? value)
        {
            return Common.NormalizeNullableText(value)?.ToUpperInvariant();
        }

        private static string NormalizeEnum(string? value, string fieldName, HashSet<string> allowedValues, int maxLength)
        {
            var normalized = NormalizeRequired(value, fieldName, maxLength).ToUpperInvariant();
            if (!allowedValues.Contains(normalized))
                throw new ArgumentException($"{fieldName} is invalid");

            return normalized;
        }

        private static int NormalizeFlag(int value, int defaultValue = 0)
        {
            return value == 0 || value == 1 ? value : defaultValue;
        }
    }
}
