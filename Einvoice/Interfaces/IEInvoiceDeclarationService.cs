using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceDeclarationService
    {
        Task<IReadOnlyList<EInvoiceDeclarationDto>> SearchAsync(string companyCd, EInvoiceDeclarationSearchRequest request);

        Task<EInvoiceDeclarationDto?> GetByIdAsync(string companyCd, long tkhaiId);

        Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetTransmissionMessagesAsync(string companyCd, long tkhaiId);

        Task<EInvoiceDeclarationDto> CreateAsync(string companyCd, string userId, EInvoiceDeclarationSaveRequest request);

        Task<EInvoiceDeclarationDto> UpdateAsync(string companyCd, string userId, long tkhaiId, EInvoiceDeclarationSaveRequest request);

        Task<EInvoiceDeclarationSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long tkhaiId);

        Task<EInvoiceDeclarationDto> SaveSignatureAsync(string companyCd, string userId, long tkhaiId, EInvoiceDeclarationSignRequest request);

        Task<int> DeleteAsync(string companyCd, string userId, long tkhaiId);

        Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> tkhaiIds);
    }
}
