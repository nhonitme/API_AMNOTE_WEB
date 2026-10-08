using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services
{
    public class MailSettingService : IMailSettingService
    {
        public const string SystemCompanyCd = "";
        public const string EInvoiceMailCd = "EINV";

        private static readonly HashSet<string> SecurityTypes = new(StringComparer.OrdinalIgnoreCase) { "NONE", "SSL", "STARTTLS" };
        private static readonly HashSet<string> AuthTypes = new(StringComparer.OrdinalIgnoreCase) { "NONE", "PASSWORD", "OAUTH2" };

        private readonly IMailSettingRepository _repository;
        private readonly IPasswordCipher _passwordCipher;
        private readonly IMailService _mailService;
        private readonly ILogger<MailSettingService> _logger;

        public MailSettingService(
            IMailSettingRepository repository,
            IPasswordCipher passwordCipher,
            IMailService mailService,
            ILogger<MailSettingService> logger)
        {
            _repository = repository;
            _passwordCipher = passwordCipher;
            _mailService = mailService;
            _logger = logger;
        }

        public async Task<MailSettingDto> GetEInvoiceMailSettingAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCdOrThrow(companyCd);
            var companySetting = await _repository.GetSettingAsync(normalizedCompanyCd, EInvoiceMailCd);
            var systemDefault = await _repository.GetSettingAsync(SystemCompanyCd, EInvoiceMailCd);
            var systemDto = systemDefault == null ? null : ToSystemPreview(systemDefault);

            if (companySetting != null)
            {
                var dto = ToDto(companySetting);
                dto.HAS_COMPANY_SETTING = true;
                dto.IS_USING_SYSTEM_DEFAULT = false;
                dto.SYSTEM_SETTING = systemDto;
                return dto;
            }

            if (systemDefault == null)
            {
                var empty = CreateEmptyCompanyTemplate(normalizedCompanyCd);
                empty.SYSTEM_SETTING = systemDto;
                return empty;
            }

            var template = ToDto(systemDefault);
            template.MAIL_ID = 0;
            template.COMPANY_CD = normalizedCompanyCd;
            template.HAS_PASSWORD = false;
            template.HAS_COMPANY_SETTING = false;
            template.IS_USING_SYSTEM_DEFAULT = true;
            template.IS_DEFAULT = 0;
            template.SYSTEM_SETTING = systemDto;
            return template;
        }

        public Task<MailSettingDto> SaveEInvoiceMailSettingAsync(string companyCd, string userId, MailSettingSaveRequest request)
        {
            var normalizedCompanyCd = NormalizeCompanyCdOrThrow(companyCd);
            if (request?.USE_SYSTEM_DEFAULT == true)
            {
                return UseSystemDefaultAsync(normalizedCompanyCd, userId);
            }

            return SaveMailSettingAsync(normalizedCompanyCd, EInvoiceMailCd, userId, request);
        }

        private async Task<MailSettingDto> UseSystemDefaultAsync(string companyCd, string userId)
        {
            await _repository.SoftDeleteSettingAsync(companyCd, EInvoiceMailCd, userId);
            _logger.LogInformation("Mail setting switched to system default: companyCd={CompanyCd}", companyCd);
            return await GetEInvoiceMailSettingAsync(companyCd);
        }

        public async Task SendTestEInvoiceMailAsync(string companyCd, MailSettingTestRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedCompanyCd = NormalizeCompanyCdOrThrow(companyCd);
            var toEmail = Common.NormalizeRequiredText(request.TO_EMAIL);
            if (string.IsNullOrWhiteSpace(toEmail) || !Common.IsValidEmail(toEmail))
            {
                throw new ArgumentException("TO_EMAIL is invalid");
            }

            var existing = await _repository.GetSettingWithSecretAsync(normalizedCompanyCd, EInvoiceMailCd);
            var systemDefault = await _repository.GetSettingWithSecretAsync(SystemCompanyCd, EInvoiceMailCd);
            var authType = NormalizeAuthType(request.AUTH_TYPE);
            var updatePassword = !string.IsNullOrWhiteSpace(request.PASSWORD);

            if (authType == "OAUTH2")
            {
                throw new NotSupportedException("OAUTH2 mail authentication is not supported yet");
            }

            var fromEmail = Common.NormalizeRequiredText(request.FROM_EMAIL) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fromEmail) || !Common.IsValidEmail(fromEmail))
            {
                throw new ArgumentException("FROM_EMAIL is invalid");
            }

            var setting = new MailSetting
            {
                COMPANY_CD = normalizedCompanyCd,
                MAIL_CD = EInvoiceMailCd,
                MAIL_NM = Common.NormalizeRequiredText(request.MAIL_NM) ?? "E-invoice mail",
                SMTP_HOST = Common.NormalizeRequiredText(request.SMTP_HOST) ?? string.Empty,
                SMTP_PORT = request.SMTP_PORT > 0 ? request.SMTP_PORT : 587,
                SECURITY_TYPE = NormalizeSecurityType(request.SECURITY_TYPE),
                AUTH_TYPE = authType,
                USERNAME = Common.NormalizeNullableText(request.USERNAME),
                FROM_EMAIL = fromEmail,
                FROM_NAME = Common.NormalizeNullableText(request.FROM_NAME),
                REPLY_TO_EMAIL = Common.NormalizeNullableText(request.REPLY_TO_EMAIL),
                CONFIG_JSON = SerializeConfig(new MailSendOptions
                {
                    TimeoutMs = request.CONFIG?.TimeoutMs > 0 ? request.CONFIG.TimeoutMs : 100000,
                    SendAll = true,
                    AttachPdf = false,
                    AttachXml = false,
                }),
                IS_ACTIVE = 1,
            };

            if (updatePassword)
            {
                setting.PASSWORD_ENC = _passwordCipher.Encrypt(request.PASSWORD!.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(existing?.PASSWORD_ENC))
            {
                setting.PASSWORD_ENC = existing.PASSWORD_ENC;
            }
            else if (!string.IsNullOrWhiteSpace(systemDefault?.PASSWORD_ENC))
            {
                setting.PASSWORD_ENC = systemDefault.PASSWORD_ENC;
            }

            await _mailService.SendWithSettingAsync(
                setting,
                new MailSendRequest
                {
                    To = toEmail,
                    Subject = $"[AMNOTE] Test mail - {normalizedCompanyCd}",
                    Body = $"Đây là email thử từ cấu hình mail tùy chỉnh của công ty {normalizedCompanyCd}.",
                    IsBodyHtml = false,
                },
                cancellationToken);

            _logger.LogInformation("Test mail sent. CompanyCd={CompanyCd}, To={To}", normalizedCompanyCd, toEmail);
        }

        private async Task<MailSettingDto> SaveMailSettingAsync(
            string companyCd,
            string mailCd,
            string userId,
            MailSettingSaveRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrEmpty(companyCd))
            {
                throw new InvalidOperationException("System mail setting cannot be modified from this screen");
            }

            var existing = await _repository.GetSettingWithSecretAsync(companyCd, mailCd);
            var deletedExisting = existing == null
                ? await _repository.GetSettingIncludingDeletedAsync(companyCd, mailCd)
                : null;
            var restoringDeleted = deletedExisting != null && deletedExisting.ISDEL == 1;
            var systemDefault = await _repository.GetSettingWithSecretAsync(SystemCompanyCd, mailCd);
            var authType = NormalizeAuthType(request.AUTH_TYPE);
            var updatePassword = !string.IsNullOrWhiteSpace(request.PASSWORD);

            if (authType == "PASSWORD" && !updatePassword && existing == null && !restoringDeleted)
            {
                var hasSystemPassword = !string.IsNullOrWhiteSpace(systemDefault?.PASSWORD_ENC);
                if (!hasSystemPassword)
                {
                    throw new ArgumentException("SMTP password is required");
                }
            }

            if (authType == "OAUTH2")
            {
                throw new NotSupportedException("OAUTH2 mail authentication is not supported yet");
            }

            var fromEmail = Common.NormalizeRequiredText(request.FROM_EMAIL) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromEmail) && !Common.IsValidEmail(fromEmail))
            {
                throw new ArgumentException("FROM_EMAIL is invalid");
            }

            var replyToEmail = Common.NormalizeNullableText(request.REPLY_TO_EMAIL);
            if (!string.IsNullOrWhiteSpace(replyToEmail) && !Common.IsValidEmail(replyToEmail))
            {
                throw new ArgumentException("REPLY_TO_EMAIL is invalid");
            }

            var entity = new MailSetting
            {
                MAIL_ID = request.MAIL_ID > 0 ? request.MAIL_ID : existing?.MAIL_ID ?? 0,
                COMPANY_CD = companyCd,
                MAIL_CD = mailCd,
                MAIL_NM = Common.NormalizeRequiredText(request.MAIL_NM) ?? "E-invoice mail",
                SMTP_HOST = Common.NormalizeRequiredText(request.SMTP_HOST) ?? string.Empty,
                SMTP_PORT = request.SMTP_PORT > 0 ? request.SMTP_PORT : 587,
                SECURITY_TYPE = NormalizeSecurityType(request.SECURITY_TYPE),
                AUTH_TYPE = authType,
                USERNAME = Common.NormalizeNullableText(request.USERNAME),
                FROM_EMAIL = fromEmail,
                FROM_NAME = Common.NormalizeNullableText(request.FROM_NAME),
                REPLY_TO_EMAIL = replyToEmail,
                CONFIG_JSON = SerializeConfig(request.CONFIG),
                IS_DEFAULT = 0,
                IS_ACTIVE = request.IS_ACTIVE == 0 ? 0 : 1,
            };

            if (updatePassword)
            {
                entity.PASSWORD_ENC = _passwordCipher.Encrypt(request.PASSWORD!.Trim());
            }
            else if (existing != null)
            {
                entity.PASSWORD_ENC = existing.PASSWORD_ENC;
            }
            else if (restoringDeleted)
            {
                entity.PASSWORD_ENC = null;
            }
            else if (!string.IsNullOrWhiteSpace(systemDefault?.PASSWORD_ENC))
            {
                entity.PASSWORD_ENC = systemDefault.PASSWORD_ENC;
                updatePassword = true;
            }

            var saved = await _repository.SaveSettingAsync(companyCd, mailCd, userId, entity, updatePassword)
                ?? throw new InvalidOperationException("Mail setting was saved but could not be reloaded");

            _logger.LogInformation("Mail setting saved: companyCd={CompanyCd}, mailCd={MailCd}, mailId={MailId}", companyCd, mailCd, saved.MAIL_ID);
            return await GetEInvoiceMailSettingAsync(companyCd);
        }

        private static MailSettingDto CreateEmptyCompanyTemplate(string companyCd)
        {
            return new MailSettingDto
            {
                MAIL_ID = 0,
                COMPANY_CD = companyCd,
                MAIL_CD = EInvoiceMailCd,
                MAIL_NM = "E-invoice mail",
                SMTP_PORT = 587,
                SECURITY_TYPE = "STARTTLS",
                AUTH_TYPE = "PASSWORD",
                CONFIG = new MailSendOptions(),
                IS_DEFAULT = 0,
                IS_ACTIVE = 1,
                HAS_COMPANY_SETTING = false,
                IS_USING_SYSTEM_DEFAULT = true,
            };
        }

        private static string NormalizeCompanyCdOrThrow(string? companyCd)
        {
            var normalized = Common.NormalizeRequiredText(companyCd) ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                throw new ArgumentException("Company code is required");
            }

            return normalized;
        }

        private MailSettingDto ToSystemPreview(MailSetting entity)
        {
            var dto = ToDto(entity);
            dto.HAS_COMPANY_SETTING = false;
            dto.IS_USING_SYSTEM_DEFAULT = true;
            dto.SYSTEM_SETTING = null;
            return dto;
        }

        private MailSettingDto ToDto(MailSetting entity)
        {
            var hasPassword = entity.HAS_PASSWORD == 1 || !string.IsNullOrWhiteSpace(entity.PASSWORD_ENC);

            return new MailSettingDto
            {
                MAIL_ID = entity.MAIL_ID,
                COMPANY_CD = entity.COMPANY_CD ?? string.Empty,
                MAIL_CD = entity.MAIL_CD ?? EInvoiceMailCd,
                MAIL_NM = entity.MAIL_NM ?? string.Empty,
                SMTP_HOST = entity.SMTP_HOST ?? string.Empty,
                SMTP_PORT = entity.SMTP_PORT > 0 ? entity.SMTP_PORT : 587,
                SECURITY_TYPE = NormalizeSecurityType(entity.SECURITY_TYPE),
                AUTH_TYPE = NormalizeAuthType(entity.AUTH_TYPE),
                USERNAME = entity.USERNAME,
                HAS_PASSWORD = hasPassword,
                FROM_EMAIL = entity.FROM_EMAIL ?? string.Empty,
                FROM_NAME = entity.FROM_NAME,
                REPLY_TO_EMAIL = entity.REPLY_TO_EMAIL,
                CONFIG = _mailService.ParseOptions(entity.CONFIG_JSON),
                IS_DEFAULT = entity.IS_DEFAULT,
                IS_ACTIVE = entity.IS_ACTIVE,
            };
        }

        private static string? SerializeConfig(MailSendOptions? config)
        {
            if (config == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
        }

        private static string NormalizeSecurityType(string? value)
        {
            var normalized = Common.NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized != null && SecurityTypes.Contains(normalized) ? normalized : "STARTTLS";
        }

        private static string NormalizeAuthType(string? value)
        {
            var normalized = Common.NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized != null && AuthTypes.Contains(normalized) ? normalized : "PASSWORD";
        }
    }
}
