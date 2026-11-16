using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitXslService
{
    Task<IReadOnlyList<PitXslTemplate>> ListAsync(string companyCd, string? templateCd = null);

    Task<IReadOnlyList<PitXslTemplate>> ListActiveCatalogAsync(string companyCd);

    Task<PitXslTemplate> GetAsync(string companyCd, long xslId);

    Task<PitXslTemplate> RequireActiveCatalogAsync(string companyCd, long xslId);

    Task<PitXslTemplate> SaveAsync(string companyCd, string userId, long xslId, PitXslTemplateSaveRequest request);

    Task DeleteAsync(string companyCd, string userId, long xslId);

    Task<PitXslImageResult> UploadImageAsync(
        string companyCd,
        string userId,
        long xslId,
        string imageKind,
        string originalFileName,
        byte[] bytes,
        CancellationToken cancellationToken = default);

    Task<PitXslImageResult> SelectImageAsync(
        string companyCd,
        string userId,
        long xslId,
        string imageKind,
        string? fileName,
        string? path);

    Task<string> PreviewHtmlAsync(string companyCd, long xslId);

    Task<byte[]> PreviewPdfAsync(
        string companyCd,
        long xslId,
        CancellationToken cancellationToken = default);

    Task<PitXslTemplate> ResolveForPrintAsync(string companyCd, long? xslId);

    Task<string> TransformAsync(string companyCd, string xml, PitXslTemplate template);
}
