using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitErrorNoticeService
{
    Task<long> SaveAsync(string company, string user, long id, int version, PitErrorNoticeDto data);
}
