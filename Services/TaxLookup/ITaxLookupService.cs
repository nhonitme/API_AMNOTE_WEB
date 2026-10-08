using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services.TaxLookup
{
    public interface ITaxLookupService
    {
        Task<TaxLookupInfoDto?> LookupAsync(string mst, CancellationToken cancellationToken = default);
    }
}
