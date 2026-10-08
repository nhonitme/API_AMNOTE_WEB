using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services
{
    public class MailService : IMailService
    {
        private readonly IMailSettingRepository _mailSettingRepository;
        private readonly IPasswordCipher _passwordCipher;
        private readonly ILogger<MailService> _logger;

        public MailService(
            IMailSettingRepository mailSettingRepository,
            IPasswordCipher passwordCipher,
            ILogger<MailService> logger)
        {
            _mailSettingRepository = mailSettingRepository;
            _passwordCipher = passwordCipher;
            _logger = logger;
        }

        public async Task SendAsync(
            string companyCd,
            string mailCd,
            MailSendRequest request,
            CancellationToken cancellationToken = default)
        {
            var setting = await _mailSettingRepository.GetActiveSettingAsync(companyCd, mailCd)
                ?? throw new InvalidOperationException($"Mail setting '{mailCd}' is not configured");

            await SendWithSettingAsync(setting, request, cancellationToken);
        }

        public async Task SendWithSettingAsync(
            MailSetting setting,
            MailSendRequest request,
            CancellationToken cancellationToken = default)
        {
            if (setting == null)
            {
                throw new ArgumentNullException(nameof(setting));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var to = Common.NormalizeRequiredText(request.To)?.Replace(",", ";", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException("Recipient email is required");
            }

            EInvoiceBuyerEmailHelper.ValidateBuyerEmailList(to, "To");

            var subject = Common.NormalizeRequiredText(request.Subject) ?? string.Empty;
            var body = request.Body ?? string.Empty;

            ValidateSetting(setting);

            var config = ParseConfigJson(setting.CONFIG_JSON);
            var recipients = EInvoiceBuyerEmailHelper.ParseBuyerEmails(to);
            if (recipients.Count == 0)
            {
                throw new ArgumentException("Recipient email is required");
            }

            var cc = ResolveRecipients(request.Cc, config.Cc);
            var bcc = ResolveRecipients(request.Bcc, config.Bcc);
            var attachmentPayloads = CloneAttachments(request.Attachments);
            var errors = new List<string>();

            if (config.SendAll)
            {
                try
                {
                    await SendOneAsync(
                        setting,
                        config,
                        recipients,
                        cc,
                        bcc,
                        subject,
                        body,
                        request.IsBodyHtml,
                        attachmentPayloads,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    throw WrapSendException(setting, string.Join(";", recipients), ex);
                }
            }
            else
            {
                foreach (var recipient in recipients)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await SendOneAsync(
                            setting,
                            config,
                            [recipient],
                            cc,
                            bcc,
                            subject,
                            body,
                            request.IsBodyHtml,
                            attachmentPayloads,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{recipient}: {UnwrapSendException(ex)}");
                    }
                }

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(string.Join("; ", errors));
                }
            }

            _logger.LogInformation(
                "Mail sent. CompanyCd={CompanyCd}, MailCd={MailCd}, To={To}, Subject={Subject}",
                setting.COMPANY_CD,
                setting.MAIL_CD,
                to,
                subject);
        }

        public MailSendOptions ParseOptions(string? configJson)
            => ParseConfigJson(configJson);

        private async Task SendOneAsync(
            MailSetting setting,
            MailSendOptions config,
            IReadOnlyList<string> recipients,
            string? cc,
            string? bcc,
            string subject,
            string body,
            bool isBodyHtml,
            IReadOnlyList<MailAttachmentDto> attachmentPayloads,
            CancellationToken cancellationToken)
        {
            using var message = BuildMailMessage(
                setting,
                recipients,
                cc,
                bcc,
                subject,
                body,
                isBodyHtml,
                attachmentPayloads);
            using var client = BuildSmtpClient(setting, config.TimeoutMs);

            var previousProtocol = ServicePointManager.SecurityProtocol;
            try
            {
                ApplySecurityProtocol(setting.SECURITY_TYPE);
                cancellationToken.ThrowIfCancellationRequested();
                await client.SendMailAsync(message, cancellationToken);
            }
            finally
            {
                ServicePointManager.SecurityProtocol = previousProtocol;
            }
        }

        private static void ValidateSetting(MailSetting setting)
        {
            if (string.IsNullOrWhiteSpace(setting.SMTP_HOST))
            {
                throw new InvalidOperationException("SMTP host is not configured");
            }

            if (string.IsNullOrWhiteSpace(setting.FROM_EMAIL))
            {
                throw new InvalidOperationException("FROM_EMAIL is not configured");
            }

            if (!Common.IsValidEmail(setting.FROM_EMAIL))
            {
                throw new InvalidOperationException("FROM_EMAIL is invalid");
            }

            var authType = NormalizeAuthType(setting.AUTH_TYPE);
            if (authType == "OAUTH2")
            {
                throw new NotSupportedException("OAUTH2 mail authentication is not supported yet");
            }

            if (authType == "PASSWORD" && string.IsNullOrWhiteSpace(setting.USERNAME))
            {
                throw new InvalidOperationException("SMTP username is not configured");
            }

            if (setting.SMTP_HOST.Contains("sendgrid", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(setting.USERNAME?.Trim(), "apikey", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SendGrid SMTP requires USERNAME = apikey and PASSWORD = SendGrid API key");
            }
        }

        private static MailMessage BuildMailMessage(
            MailSetting setting,
            IReadOnlyList<string> recipients,
            string? cc,
            string? bcc,
            string subject,
            string body,
            bool isBodyHtml,
            IReadOnlyList<MailAttachmentDto> attachmentPayloads)
        {
            var fromName = Common.NormalizeNullableText(setting.FROM_NAME);
            var from = string.IsNullOrWhiteSpace(fromName)
                ? new MailAddress(setting.FROM_EMAIL.Trim())
                : new MailAddress(setting.FROM_EMAIL.Trim(), fromName, Encoding.UTF8);

            var message = new MailMessage
            {
                From = from,
                Subject = subject,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                IsBodyHtml = isBodyHtml,
            };

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient.Trim()));
            }

            AddRecipients(message.CC, cc);
            AddRecipients(message.Bcc, bcc);

            var replyTo = Common.NormalizeNullableText(setting.REPLY_TO_EMAIL);
            if (!string.IsNullOrWhiteSpace(replyTo) && Common.IsValidEmail(replyTo))
            {
                message.ReplyToList.Add(replyTo);
            }

            foreach (var attachment in attachmentPayloads)
            {
                if (attachment.Content.Length == 0)
                {
                    continue;
                }

                var fileName = Common.NormalizeRequiredText(attachment.FileName) ?? "attachment.bin";
                var contentType = Common.NormalizeRequiredText(attachment.ContentType) ?? MediaTypeNames.Application.Octet;
                var stream = new MemoryStream(attachment.Content, writable: false);
                message.Attachments.Add(new Attachment(stream, fileName, contentType));
            }

            return message;
        }

        private SmtpClient BuildSmtpClient(MailSetting setting, int timeoutMs)
        {
            var securityType = NormalizeSecurityType(setting.SECURITY_TYPE);
            var authType = NormalizeAuthType(setting.AUTH_TYPE);
            var port = setting.SMTP_PORT > 0 ? setting.SMTP_PORT : 587;

            var client = new SmtpClient(setting.SMTP_HOST.Trim(), port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = timeoutMs > 0 ? timeoutMs : 100000,
                UseDefaultCredentials = false,
            };

            client.EnableSsl = securityType is "SSL" or "STARTTLS";

            if (authType == "PASSWORD")
            {
                var username = Common.NormalizeRequiredText(setting.USERNAME) ?? string.Empty;
                var password = _passwordCipher.Decrypt(setting.PASSWORD_ENC);
                client.Credentials = new NetworkCredential(username, password);
            }

            return client;
        }

        private static void ApplySecurityProtocol(string? securityType)
        {
            var normalized = NormalizeSecurityType(securityType);
            ServicePointManager.SecurityProtocol = normalized switch
            {
                "NONE" => SecurityProtocolType.Tls12,
                _ => SecurityProtocolType.Tls12,
            };
        }

        private static List<MailAttachmentDto> CloneAttachments(List<MailAttachmentDto>? attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return [];
            }

            return attachments
                .Where(item => item?.Content != null && item.Content.Length > 0)
                .Select(item => new MailAttachmentDto
                {
                    FileName = item.FileName,
                    ContentType = item.ContentType,
                    Content = item.Content.ToArray(),
                })
                .ToList();
        }

        private static MailSendOptions ParseConfigJson(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return new MailSendOptions();
            }

            try
            {
                return JsonSerializer.Deserialize<MailSendOptions>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? new MailSendOptions();
            }
            catch
            {
                return new MailSendOptions();
            }
        }

        private static string? ResolveRecipients(string? requestValue, string? configValue)
        {
            var normalizedRequest = Common.NormalizeNullableText(requestValue);
            if (!string.IsNullOrWhiteSpace(normalizedRequest))
            {
                return normalizedRequest.Replace(",", ";", StringComparison.Ordinal);
            }

            return Common.NormalizeNullableText(configValue)?.Replace(",", ";", StringComparison.Ordinal);
        }

        private static void AddRecipients(MailAddressCollection collection, string? recipients)
        {
            if (string.IsNullOrWhiteSpace(recipients))
            {
                return;
            }

            foreach (var part in recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Common.IsValidEmail(part))
                {
                    collection.Add(part);
                }
            }
        }

        private static string NormalizeSecurityType(string? value)
        {
            var normalized = Common.NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized switch
            {
                "NONE" => "NONE",
                "SSL" => "SSL",
                "0" => "NONE",
                "1" => "SSL",
                "2" => "STARTTLS",
                _ => "STARTTLS",
            };
        }

        private static string NormalizeAuthType(string? value)
        {
            var normalized = Common.NormalizeNullableText(value)?.ToUpperInvariant();
            return normalized switch
            {
                "NONE" => "NONE",
                "OAUTH2" => "OAUTH2",
                _ => "PASSWORD",
            };
        }

        private static Exception WrapSendException(MailSetting setting, string recipient, Exception ex)
        {
            var message = UnwrapSendException(ex);
            if (message.Contains("verified Sender Identity", StringComparison.OrdinalIgnoreCase)
                || message.Contains("from address does not match", StringComparison.OrdinalIgnoreCase))
            {
                message =
                    $"SendGrid rejected FROM_EMAIL '{setting.FROM_EMAIL}'. " +
                    "Verify this sender identity in SendGrid and ensure USERNAME = apikey. " +
                    $"Original error: {message}";
            }

            return new InvalidOperationException($"{recipient}: {message}", ex);
        }

        private static string UnwrapSendException(Exception ex)
        {
            if (ex is SmtpException smtpException && !string.IsNullOrWhiteSpace(smtpException.StatusCode.ToString()))
            {
                return smtpException.Message;
            }

            return ex.InnerException?.Message ?? ex.Message;
        }
    }
}
