using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class MailSettingRepository : IMailSettingRepository
    {
        private const string GetActiveSettingQuery = @"
            SELECT *
            FROM mail_setting
            WHERE MAIL_CD = @p_MAIL_CD
              AND IFNULL(IS_ACTIVE, 0) = 1
              AND IFNULL(ISDEL, 0) = 0
              AND (COMPANY_CD = @p_COMPANY_CD OR IFNULL(TRIM(COMPANY_CD), '') = '')
            ORDER BY
                CASE WHEN COMPANY_CD = @p_COMPANY_CD THEN 0 ELSE 1 END,
                IFNULL(IS_DEFAULT, 0) DESC,
                MAIL_ID DESC
            LIMIT 1";

        private const string GetSettingQuery = "CALL getMailSetting(@p_COMPANY_CD, @p_MAIL_CD)";
        private const string GetSettingWithSecretQuery = @"
            SELECT *
            FROM mail_setting
            WHERE IFNULL(TRIM(COMPANY_CD), '') = @p_COMPANY_CD
              AND MAIL_CD = @p_MAIL_CD
              AND IFNULL(ISDEL, 0) = 0
            ORDER BY MAIL_ID DESC
            LIMIT 1";
        private const string GetSettingIncludingDeletedQuery = @"
            SELECT *
            FROM mail_setting
            WHERE IFNULL(TRIM(COMPANY_CD), '') = @p_COMPANY_CD
              AND MAIL_CD = @p_MAIL_CD
            ORDER BY MAIL_ID DESC
            LIMIT 1";
        private const string SoftDeleteSettingQuery = @"
            UPDATE mail_setting
            SET
              ISDEL = 1,
              UPDATE_BY = @p_USER_ID,
              UPDATE_AT = CURRENT_TIMESTAMP
            WHERE IFNULL(TRIM(COMPANY_CD), '') = @p_COMPANY_CD
              AND MAIL_CD = @p_MAIL_CD
              AND IFNULL(TRIM(COMPANY_CD), '') <> ''
              AND IFNULL(ISDEL, 0) = 0";
        private const string SetSettingQuery = "CALL setMailSetting(@p_MAIL_ID, @p_COMPANY_CD, @p_MAIL_CD, @p_MAIL_NM, @p_SMTP_HOST, @p_SMTP_PORT, @p_SECURITY_TYPE, @p_AUTH_TYPE, @p_USERNAME, @p_PASSWORD_ENC, @p_FROM_EMAIL, @p_FROM_NAME, @p_REPLY_TO_EMAIL, @p_CONFIG_JSON, @p_IS_DEFAULT, @p_IS_ACTIVE, @p_USER_ID)";

        private readonly DapperExecutor _db;

        public MailSettingRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<MailSetting?> GetActiveSettingAsync(string companyCd, string mailCd)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd) ?? string.Empty;
            var normalizedMailCd = Common.NormalizeRequiredText(mailCd) ?? "DEFAULT";

            return (await _db.QueryAsync<MailSetting>(
                Net_DB.Net_DB_Manager,
                GetActiveSettingQuery,
                new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd.ToUpperInvariant(),
                })).FirstOrDefault();
        }

        public async Task<MailSetting?> GetSettingAsync(string companyCd, string mailCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedMailCd = NormalizeMailCd(mailCd);

            return (await _db.QueryAsync<MailSetting>(
                Net_DB.Net_DB_Manager,
                GetSettingQuery,
                new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd,
                })).FirstOrDefault();
        }

        public async Task<MailSetting?> GetSettingWithSecretAsync(string companyCd, string mailCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedMailCd = NormalizeMailCd(mailCd);

            return (await _db.QueryAsync<MailSetting>(
                Net_DB.Net_DB_Manager,
                GetSettingWithSecretQuery,
                new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd,
                })).FirstOrDefault();
        }

        public async Task<MailSetting?> GetSettingIncludingDeletedAsync(string companyCd, string mailCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedMailCd = NormalizeMailCd(mailCd);

            return (await _db.QueryAsync<MailSetting>(
                Net_DB.Net_DB_Manager,
                GetSettingIncludingDeletedQuery,
                new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd,
                })).FirstOrDefault();
        }

        public async Task SoftDeleteSettingAsync(string companyCd, string mailCd, string userId)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            if (string.IsNullOrEmpty(normalizedCompanyCd))
            {
                throw new InvalidOperationException("System mail setting cannot be deleted");
            }

            var normalizedMailCd = NormalizeMailCd(mailCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId) ?? "system";

            await _db.ExecuteAsync(
                Net_DB.Net_DB_Manager,
                SoftDeleteSettingQuery,
                new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd,
                    p_USER_ID = normalizedUserId,
                });
        }

        public async Task<MailSetting?> SaveSettingAsync(
            string companyCd,
            string mailCd,
            string userId,
            MailSetting entity,
            bool updatePassword)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedMailCd = NormalizeMailCd(mailCd);
            var normalizedUserId = Common.NormalizeRequiredText(userId) ?? "system";

            return (await _db.QueryAsync<MailSetting>(
                Net_DB.Net_DB_Manager,
                SetSettingQuery,
                new
                {
                    p_MAIL_ID = entity.MAIL_ID,
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_MAIL_CD = normalizedMailCd,
                    p_MAIL_NM = entity.MAIL_NM ?? string.Empty,
                    p_SMTP_HOST = entity.SMTP_HOST ?? string.Empty,
                    p_SMTP_PORT = entity.SMTP_PORT > 0 ? entity.SMTP_PORT : 587,
                    p_SECURITY_TYPE = entity.SECURITY_TYPE ?? "STARTTLS",
                    p_AUTH_TYPE = entity.AUTH_TYPE ?? "PASSWORD",
                    p_USERNAME = entity.USERNAME,
                    p_PASSWORD_ENC = updatePassword ? entity.PASSWORD_ENC : null,
                    p_FROM_EMAIL = entity.FROM_EMAIL ?? string.Empty,
                    p_FROM_NAME = entity.FROM_NAME,
                    p_REPLY_TO_EMAIL = entity.REPLY_TO_EMAIL,
                    p_CONFIG_JSON = entity.CONFIG_JSON,
                    p_IS_DEFAULT = entity.IS_DEFAULT,
                    p_IS_ACTIVE = entity.IS_ACTIVE,
                    p_USER_ID = normalizedUserId,
                })).FirstOrDefault();
        }

        private static string NormalizeCompanyCd(string? companyCd)
            => Common.NormalizeRequiredText(companyCd) ?? string.Empty;

        private static string NormalizeMailCd(string? mailCd)
            => (Common.NormalizeRequiredText(mailCd) ?? "DEFAULT").ToUpperInvariant();
    }
}
