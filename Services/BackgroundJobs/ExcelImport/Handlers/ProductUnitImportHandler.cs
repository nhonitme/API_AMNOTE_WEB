using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ProductUnitImportHandler : IExcelImportModuleHandler
    {
        private readonly IProductUnitService _service;

        public ProductUnitImportHandler(IProductUnitService service)
        {
            _service = service;
        }

        public string ModuleCd => "ProductUnit";

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            var records = rows.Select(row => new ProductUnit
            {
                COMPANY_CD = companyCd,
                UNIT_CD = Common.NormalizeRequiredText(Common.GetString(row, "UNIT_CD")),
                UNIT_NM = Common.NormalizeNullableText(Common.GetString(row, "UNIT_NM")),
                ISDEL = Common.NormalizeFlagString(Common.GetString(row, "ISDEL"), "0"),
                CREATE_BY = userId,
                UPDATE_BY = userId
            }).ToList();

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var inserted = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (inserted <= 0)
            {
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));
            }

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }
    }
}
