using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceRepository
    {
        Task<IEnumerable<EInvoiceInfo>> GetHeadersAsync(string companyCd, long? invoiceId, DateTime? fromDate, DateTime? toDate, string? keyword, string? dbName = null);

        Task<(IReadOnlyList<EInvoiceInfo> Items, int TotalRecords)> GetHeadersPagedAsync(
            string companyCd,
            long? invoiceId,
            DateTime? fromDate,
            DateTime? toDate,
            string? keyword,
            int pageNumber = 1,
            int pageSize = 20,
            bool includeDetails = false,
            EInvoiceSearchRequest? filters = null,
            string? dbName = null);

        Task<EInvoiceDetailsBundle> GetDetailsBundleAsync(string companyCd, IEnumerable<long> invoiceIds, string? dbName = null);

        Task<IEnumerable<EInvoiceDetail>> GetDetailsAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<IEnumerable<EInvoiceDetailSpecialInfo>> GetDetailSpecialsAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<EInvoicePxkInfo?> GetPxkAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<EInvoiceRelatedInfo?> GetRelatedAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<EInvoiceBkeInfo?> GetBkeByInvoiceAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<string> GetNextBkeNoAsync(string companyCd, int? year = null, string? dbName = null);

        Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceInfo invoice);

        Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceDetail detail);

        Task<long> SetDetailSpecialAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            long detailId,
            EInvoiceDetailSpecialInfo special);

        Task<int> DeleteDetailSpecialByInvoiceAsync(DapperSession session, string companyCd, long invoiceId);

        Task<long> SetPxkAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoicePxkInfo pxk);

        Task<int> DeletePxkByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId);

        Task<long> SetRelatedAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceRelatedInfo related);

        Task<int> DeleteRelatedByInvoiceAsync(DapperSession session, string companyCd, long invoiceId);

        Task<long> SetBkeInfoAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceBkeInfo bke);

        Task SetBkeReasonAsync(DapperSession session, long bkeId, EInvoiceBkeReason reason);

        Task SetBkeDetailAsync(DapperSession session, long bkeId, EInvoiceBkeDetail detail);

        Task DeleteBkeChildrenAsync(DapperSession session, long bkeId);

        Task DeleteBkeByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId);

        Task SetBkeSignatureAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string signedXml,
            int isSigned);

        Task<int> SetSignatureAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string xmlFtpPath,
            string? mtdiep,
            int? isSigned,
            string? errorMessage);

        Task<int> SetShdonAsync(DapperSession session, string companyCd, string userId, long invoiceId, string shdon, DateTime nlap);

        Task<int> RevertSigningReservationAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string? shdon,
            DateTime? nlap);

        Task<EInvoiceSigningSequence> GetSigningSequenceAsync(
            DapperSession session,
            string companyCd,
            long invoiceId,
            string? khmsHDON,
            string? khhdon);

        Task<int> DeleteDetailsByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId);

        Task<int> DeleteAsync(DapperSession session, string companyCd, long invoiceId, string userId);

        Task<int> GetTchdonAsync(string companyCd, long invoiceId, string? dbName = null);

        Task<int> UpdateBuyerEmailAsync(DapperSession session, string companyCd, string userId, long invoiceId, string? buyerEmail);

        Task<int> SetMailStatusAsync(DapperSession session, string companyCd, string userId, long invoiceId, int mailStatus);
    }
}
