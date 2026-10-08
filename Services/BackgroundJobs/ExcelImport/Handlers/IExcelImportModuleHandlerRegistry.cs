namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public interface IExcelImportModuleHandlerRegistry
    {
        IExcelImportModuleHandler GetHandler(string moduleCd);
    }
}
