using System.Threading;
using System.Threading.Tasks;
namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public interface IExcelImportJobQueue
{
    ValueTask EnqueueAsync(ExcelImportJobRequest request, CancellationToken cancellationToken = default);
    ValueTask<ExcelImportJobRequest> DequeueAsync(CancellationToken cancellationToken = default);
}
