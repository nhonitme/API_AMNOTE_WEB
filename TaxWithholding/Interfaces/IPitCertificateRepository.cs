using API_AMNOTE_WEB.TaxWithholding;
using API_AMNOTE_WEB.Data;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitCertificateRepository
{
    Task<(IReadOnlyList<PitCertificate> First, IReadOnlyList<PitCountRow> Second)> SearchAsync(object parameters, string? dbName);

    Task<long> SaveAsync(DapperSession s, string company, string user, long id, int version, long? xslId, PitCertificateDto d);
}
