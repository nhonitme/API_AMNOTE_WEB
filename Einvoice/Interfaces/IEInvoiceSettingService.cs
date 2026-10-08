using API_AMNOTE_WEB.Models;
namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceSettingService
    {
        Task<IReadOnlyList<EInvoiceDecimalSettingDto>> SearchDecimalSettingsAsync(string companyCd, EInvoiceDecimalSettingSearchRequest request);
        Task<EInvoiceDecimalSettingDto?> GetDecimalSettingByIdAsync(string companyCd, long settingId);
        Task<EInvoiceDecimalSettingDto> SaveDecimalSettingAsync(string companyCd, string userId, EInvoiceDecimalSettingSaveRequest request);
        Task<IReadOnlyList<EInvoiceUserSettingDto>> SearchUserSettingsAsync(string companyCd, EInvoiceUserSettingSearchRequest request);
        Task<EInvoiceUserSettingDto?> GetUserSettingByIdAsync(string companyCd, long settingId);
        Task<EInvoiceUserSettingDto> SaveUserSettingAsync(string companyCd, string userId, EInvoiceUserSettingSaveRequest request);
        Task<IReadOnlyList<EInvoiceAdminSettingDto>> SearchAdminSettingsAsync(string companyCd, EInvoiceAdminSettingSearchRequest request);
        Task<EInvoiceAdminSettingDto?> GetAdminSettingByIdAsync(string companyCd, long settingId);

        Task<IReadOnlyList<EInvoiceTemplateDesignerDesign>> GetTemplateDesignerDesignsAsync(string companyCd, long xslId);
        Task<EInvoiceTemplateDesignerDesign> CloneTemplateDesignerAsync(string companyCd, string userId, long xslId, long designId);
        Task<EInvoiceTemplateDesignerDraft> SaveTemplateDesignerDraftAsync(string companyCd, string userId, long xslId, EInvoiceTemplateDesignerDraftSaveRequest request);
        Task<EInvoiceSellerXslTemplateContentResult> PublishTemplateDesignerAsync(string companyCd, string userId, long xslId, long designId);
        Task<EInvoiceDesignerImageResult> UploadTemplateDesignerImageAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string imageKind,
            string originalFileName,
            byte[] bytes,
            CancellationToken cancellationToken = default);
        Task<EInvoiceDesignerImageResult> SelectTemplateDesignerImageAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string imageKind,
            string? fileName,
            string? path = null);
        Task<IReadOnlyList<EInvoiceFtpImageFile>> ListTemplateDesignerImagesAsync(string companyCd, string imageKind, CancellationToken cancellationToken = default);
        Task<(byte[] Bytes, string ContentType)> GetTemplateDesignerImageFileAsync(
            string companyCd,
            string imageKind,
            string? fileName,
            string? path,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EInvoiceFtpXslFile>> ListTemplateDesignerXslSamplesAsync(CancellationToken cancellationToken = default);
        Task<EInvoiceFtpXslContentResult> GetTemplateDesignerXslSampleFileAsync(string? fileName, CancellationToken cancellationToken = default);
        Task<EInvoiceSellerXslTemplateMetaResult> CreateSellerXslTemplateAsync(string companyCd, string userId, EInvoiceSellerXslTemplateMetaSaveRequest request, CancellationToken cancellationToken = default);
        Task<EInvoiceSellerXslTemplateMetaResult> UpdateSellerXslTemplateAsync(string companyCd, string userId, long xslId, EInvoiceSellerXslTemplateMetaSaveRequest request, CancellationToken cancellationToken = default);
        Task<int> DeleteSellerXslTemplatesAsync(string companyCd, string userId, IEnumerable<long> xslIds);
        Task<EInvoiceSellerDto> UpdateSellerInfoAsync(string companyCd, string userId, long sellerId, EInvoiceSellerInfoSaveRequest request);
    }
}
