using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using System.Threading;
using System.Threading.Tasks;
namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public interface IExcelImportJobProcessor
{
    Task<ExcelImportResultDto> ProcessAsync(
        ExcelImportJobRequest request,
        IExcelImportJobProgressWriter progressWriter,
        IExcelImportModuleHandler handler,
        CancellationToken cancellationToken = default);
}
