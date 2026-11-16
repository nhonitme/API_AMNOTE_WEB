using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitDeclarationService
{
    Task<long> SaveAsync(string company, string user, long id, int version, PitDeclarationDto data);
}
