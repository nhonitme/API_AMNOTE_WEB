using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMailSettingRepository
    {
        Task<MailSetting?> GetActiveSettingAsync(string companyCd, string mailCd);

        Task<MailSetting?> GetSettingAsync(string companyCd, string mailCd);

        Task<MailSetting?> GetSettingWithSecretAsync(string companyCd, string mailCd);

        Task<MailSetting?> GetSettingIncludingDeletedAsync(string companyCd, string mailCd);

        Task SoftDeleteSettingAsync(string companyCd, string mailCd, string userId);

        Task<MailSetting?> SaveSettingAsync(
            string companyCd,
            string mailCd,
            string userId,
            MailSetting entity,
            bool updatePassword);
    }
}
