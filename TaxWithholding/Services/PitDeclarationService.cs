using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitDeclarationService(DapperExecutor db, IPitDeclarationRepository repository) : IPitDeclarationService
{
    public async Task<long> SaveAsync(string company, string user, long id, int version, PitDeclarationDto data)
    {
        PitDeclarationValidator.Validate(data);
        await using var session = await db.CreateSessionAsync();
        var savedId = await repository.SaveAsync(session, company, user, id, version, data);
        session.Commit();
        return savedId;
    }
}
