using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceDeclarationRepository
    {
        Task<IEnumerable<EInvoiceDeclarationInfo>> GetHeadersAsync(string companyCd, long? tkhaiId, DateTime? fromDate, DateTime? toDate, string? keyword, int? isSigned);

        Task<IEnumerable<EInvoiceDeclarationDetail>> GetDetailsAsync(string companyCd, long tkhaiId);

        Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceDeclarationInfo declaration);

        Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long tkhaiId, EInvoiceDeclarationDetail detail);

        Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long tkhaiId, string signedXml, int isSigned, string? errorMessage);

        Task<int> DeleteDetailsByDeclarationAsync(DapperSession session, string companyCd, long tkhaiId, string userId);

        Task<int> DeleteAsync(DapperSession session, string companyCd, long tkhaiId, string userId);
    }
}
