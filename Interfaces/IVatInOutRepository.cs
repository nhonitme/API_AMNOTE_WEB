using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces;

public interface IVatInOutRepository
{
    Task<(IReadOnlyList<VatInOutInvoice> Items, int TotalRecords)> GetPagedAsync(
        string companyCd, VatInOutQuery filter, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);
}
