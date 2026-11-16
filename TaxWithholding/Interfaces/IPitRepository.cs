using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitRepository
{
    Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        long id = 0,
        DateTime? from = null,
        DateTime? to = null,
        string? keyword = null,
        int? signed = null,
        int? cqtStatus = null,
        int pageNumber = 0,
        int pageSize = 0,
        string? dbName = null);

    Task<PitDocument> Get(string company, string kind, long id, string? dbName = null);

    Task Delete(string company, string user, string kind, long id, int version);

    Task<PitDocument> Prepare(string company, string kind, long id);

    Task Sign(string company, string user, string kind, long id, PitSignRequest request);
}
