using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceMinuteRepository
    {
        Task<IEnumerable<EInvoiceMinuteInfo>> GetHeadersAsync(string companyCd, long? bbanId, DateTime? fromDate, DateTime? toDate, string? keyword, int? isSigned, string? dbName = null);

        Task<IEnumerable<EInvoiceMinuteLine>> GetReasonsAsync(string companyCd, long bbanId, string? dbName = null);

        Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceMinuteInfo minute);

        Task<long> SetReasonAsync(DapperSession session, string companyCd, string userId, long bbanId, EInvoiceMinuteLine row);

        Task<int> DeleteReasonsByMinuteAsync(DapperSession session, string companyCd, long bbanId);

        Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long bbanId, string signedXml, string checksum);

        Task<int> SetBuyerSignatureAsync(DapperSession session, string companyCd, string userId, long bbanId, string signedXml, string checksum);

        Task<int> SetMailSentAsync(DapperSession session, string companyCd, string userId, long bbanId);

        Task<int> DeleteAsync(DapperSession session, string companyCd, long bbanId, string userId);
    }
}
