using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IGdtImportRepository
    {
        Task<GdtExistingInvoice> ReadExistingAsync(
            string invoiceType,
            string mhdon,
            CancellationToken cancellationToken = default);

        Task InsertListAsync(
            string invoiceType,
            GdtImportInvoiceRow row,
            CancellationToken cancellationToken = default);

        Task InvalidateJsonAndUpdateStatusAsync(
            string invoiceType,
            string mhdon,
            string status,
            CancellationToken cancellationToken = default);

        Task UpsertJsonAsync(
            string invoiceType,
            string mhdon,
            string json,
            CancellationToken cancellationToken = default);

        /// <summary>Giữ tương thích endpoint upsert cũ (header + json).</summary>
        Task<(int HeaderCount, int JsonCount)> UpsertAsync(
            string invoiceType,
            IReadOnlyList<GdtImportInvoiceRow> rows,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<GdtSavedLoginDto>> ListSavedLoginsAsync(
            string companyCd,
            CancellationToken cancellationToken = default);

        Task SaveLoginAsync(
            string companyCd,
            string username,
            string password,
            string? token = null,
            CancellationToken cancellationToken = default);
    }

    public interface IGdtImportService
    {
        Task<GdtImportUpsertResult> UpsertListAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default);

        Task<GdtImportUpsertResult> UpsertJsonAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default);

        Task<GdtImportUpsertResult> UpsertAsync(
            string companyCd,
            string userId,
            GdtImportUpsertRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<GdtSavedLoginDto>> ListSavedLoginsAsync(
            string companyCd,
            CancellationToken cancellationToken = default);

        Task SaveLoginAsync(
            string companyCd,
            string username,
            string password,
            string? token = null,
            CancellationToken cancellationToken = default);
    }
}
