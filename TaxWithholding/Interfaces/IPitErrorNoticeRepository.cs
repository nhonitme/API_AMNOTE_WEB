using API_AMNOTE_WEB.TaxWithholding;
using API_AMNOTE_WEB.Data;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitErrorNoticeRepository
{
    Task<(IReadOnlyList<PitErrorNotice> First, IReadOnlyList<PitCountRow> Second)> SearchAsync(object parameters, string? dbName);

    Task<long> SaveAsync(DapperSession s, string company, string user, long id, int version, PitErrorNoticeDto d);
}
