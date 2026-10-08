using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IMailSettingService
    {
        Task<MailSettingDto> GetEInvoiceMailSettingAsync(string companyCd);

        Task<MailSettingDto> SaveEInvoiceMailSettingAsync(string companyCd, string userId, MailSettingSaveRequest request);

        Task SendTestEInvoiceMailAsync(string companyCd, MailSettingTestRequest request, CancellationToken cancellationToken = default);
    }
}
