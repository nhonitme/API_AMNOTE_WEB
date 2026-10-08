using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceErrorNoticeService
    {
        Task<IReadOnlyList<EInvoiceErrorNoticeDto>> SearchAsync(string companyCd, EInvoiceErrorNoticeSearchRequest request);

        Task<EInvoiceErrorNoticeDto?> GetByIdAsync(string companyCd, long tbaoId);

        Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetTransmissionMessagesAsync(string companyCd, long tbaoId);

        Task<EInvoiceErrorNoticeDto> CreateAsync(string companyCd, string userId, EInvoiceErrorNoticeSaveRequest request);

        Task<EInvoiceErrorNoticeDto> UpdateAsync(string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeSaveRequest request);

        Task<EInvoiceErrorNoticeSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long tbaoId);

        Task<EInvoiceErrorNoticeDto> SaveSignatureAsync(string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeSignRequest request);

        Task<EInvoiceErrorNoticeSendMailResult> SendMailAsync(string companyCd, string userId, EInvoiceErrorNoticeSendMailRequest request, CancellationToken cancellationToken = default);

        Task<int> DeleteAsync(string companyCd, string userId, long tbaoId);

        Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> tbaoIds);
    }
}
