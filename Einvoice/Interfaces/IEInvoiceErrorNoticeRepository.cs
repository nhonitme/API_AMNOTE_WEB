using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceErrorNoticeRepository
    {
        Task<IEnumerable<EInvoiceErrorNoticeInfo>> GetHeadersAsync(string companyCd, long? tbaoId, DateTime? fromDate, DateTime? toDate, string? keyword, int? isSigned);

        Task<IEnumerable<EInvoiceErrorNoticeDetail>> GetDetailsAsync(string companyCd, long tbaoId);

        Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceErrorNoticeInfo notice);

        Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeDetail detail);

        Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long tbaoId, string signedXml, int isSigned, string? errorMessage);

        Task<int> SetMailSentAsync(DapperSession session, string companyCd, string userId, long tbaoId);

        Task<int> DeleteDetailsByNoticeAsync(DapperSession session, string companyCd, long tbaoId, string userId);

        Task<int> DeleteAsync(DapperSession session, string companyCd, long tbaoId, string userId);
    }
}
