using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public interface IExcelImportModuleHandler
    {
        string ModuleCd { get; }

        void ValidateRow(
            Dictionary<string, object> row,
            int rowNo,
            List<ExcelImportResultRowDto> validateResults)
        {
        }

        Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        Task SaveAsync(
            List<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            IExcelImportJobProgressWriter progressWriter,
            CancellationToken cancellationToken);
    }

    public interface IExcelImportModuleAliasProvider
    {
        IReadOnlyCollection<string> ModuleCds { get; }
    }
}
