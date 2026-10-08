namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;

public interface IExcelImportCustomProcessorHandler
{
    Task<ExcelImportResultDto> ProcessImportAsync(
        ExcelImportJobRequest request,
        IExcelImportJobProgressWriter progressWriter,
        CancellationToken cancellationToken = default);
}
