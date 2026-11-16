using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitXslTemplateRepository
{
    Task<IReadOnlyList<PitXslTemplate>> ListAsync(string companyCd, string? templateCd = null);

    Task<PitXslTemplate?> GetAsync(string companyCd, long xslId);

    Task<long> SaveAsync(string companyCd, string userId, long xslId, PitXslTemplateSaveRequest request);

    Task DeleteAsync(string companyCd, string userId, long xslId);

    Task SetImageAsync(string companyCd, string userId, long xslId, string kind, string path);
}
