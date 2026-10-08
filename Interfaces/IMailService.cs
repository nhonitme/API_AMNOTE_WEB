using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMailService
    {
        Task SendAsync(string companyCd, string mailCd, MailSendRequest request, CancellationToken cancellationToken = default);

        Task SendWithSettingAsync(MailSetting setting, MailSendRequest request, CancellationToken cancellationToken = default);

        MailSendOptions ParseOptions(string? configJson);
    }
}
