using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitCertificateService
{
    Task<long> SaveAsync(string company, string user, long id, int version, long? xslId, PitCertificateDto data);

    Task ValidateSigningAsync(string company, long id, bool acceptingSignature = false);
}
