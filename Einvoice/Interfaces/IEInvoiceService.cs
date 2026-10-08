using API_AMNOTE_WEB.Models;
namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceService
    {
        Task<EInvoiceDto> IssueMttAsync(string companyCd, string userId, long invoiceId);
        Task<EInvoiceMttBatchPayload> PrepareMttBatchAsync(string companyCd, string userId, EInvoiceMttBatchRequest request);
        Task<int> SaveMttBatchAsync(string companyCd, string userId, string batchId, EInvoiceMttBatchSignRequest request);
        Task<string> GetMttBatchXmlAsync(string companyCd, string userId, EInvoiceMttBatchRequest request);
        Task<IReadOnlyList<EInvoiceDto>> SearchAsync(string companyCd, EInvoiceSearchRequest request);
        Task<(IReadOnlyList<EInvoiceDto> Items, int TotalRecords)> SearchPagedAsync(string companyCd, EInvoiceSearchRequest request);
        Task<EInvoiceDto?> GetByIdAsync(string companyCd, long invoiceId);
        Task<IReadOnlyList<EInvoiceTransmissionMessageDto>> GetTransmissionMessagesAsync(string companyCd, long invoiceId);
        Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> GetMailHistoryAsync(string companyCd, long invoiceId);
        Task<EInvoiceDto?> LookupByCodeAsync(string companyCd, string lookupCode);
        Task<EInvoicePublicLookupDto?> LookupPublicAsync(string taxCode, string lookupCode);
        Task<EInvoicePublicDownloadInfo?> GetPublicDownloadInfoAsync(string taxCode, string lookupCode);
        Task<string?> GetPublicXmlAsync(string taxCode, string lookupCode);
        Task<string> GetXmlAsync(string companyCd, long invoiceId);
        Task<IReadOnlyList<EInvoiceSellerDto>> GetSellersAsync(string companyCd, EInvoiceSellerSearchRequest request);
        Task<string> GetNextBkeNoAsync(string companyCd, int? year = null);

        Task<EInvoiceDto> CreateAsync(string companyCd, string userId, EInvoiceSaveRequest request);
        Task<EInvoiceDto> UpdateAsync(string companyCd, string userId, long invoiceId, EInvoiceSaveRequest request);
        Task<EInvoiceDto> UpdateBuyerEmailAsync(string companyCd, string userId, long invoiceId, EInvoiceUpdateBuyerEmailRequest request);
        Task<EInvoiceSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long invoiceId);
        Task<EInvoiceDto> SaveSignatureAsync(string companyCd, string userId, long invoiceId, EInvoiceSignRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, long invoiceId);
        Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> invoiceIds);
        Task<EInvoiceSendMailResult> SendMailAsync(string companyCd, string userId, EInvoiceSendMailRequest request, CancellationToken cancellationToken = default);
    }
}
