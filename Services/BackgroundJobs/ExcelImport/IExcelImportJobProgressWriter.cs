using System.Threading;
using System.Threading.Tasks;
namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public interface IExcelImportJobProgressWriter
{
    /// <summary>
    /// Cập nhật số dòng Excel đã xử lý. Percent được tính từ processedRows/totalRows.
    /// </summary>
    Task ReportAsync(int processedRows, int totalRows, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật số dòng Excel và Percent tường minh (tiến trình theo giai đoạn).
    /// </summary>
    Task ReportAsync(int processedRows, int totalRows, int percent, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chỉ cập nhật Percent/message, giữ nguyên số dòng Excel đã báo cáo.
    /// </summary>
    Task ReportPercentAsync(int percent, string? message = null, CancellationToken cancellationToken = default);
}
