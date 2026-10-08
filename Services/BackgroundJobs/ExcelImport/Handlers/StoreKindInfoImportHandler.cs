using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class StoreKindInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "StoreKindInfo";
        private readonly IStoreKindInfoService _service;

        public StoreKindInfoImportHandler(IStoreKindInfoService service)
        {
            _service = service;
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;

            var records = rows.Select(row => new StoreKindInfoRequest
            {
                STORE_KIND_CD = Common.NormalizeRequiredText(Common.GetString(row, "STORE_KIND_CD")),
                STORE_KIND_ID = 0,
                STORE_KIND_NM_VIET = Common.NormalizeNullableText(Common.GetString(row, "STORE_KIND_NM_VIET")) ?? string.Empty,
                STORE_KIND_NM_ENG = Common.NormalizeNullableText(Common.GetString(row, "STORE_KIND_NM_ENG")) ?? string.Empty,
                STORE_KIND_NM_KOR = Common.NormalizeNullableText(Common.GetString(row, "STORE_KIND_NM_KOR")) ?? string.Empty,
                STORE_KIND_NM_CHINA = Common.NormalizeNullableText(Common.GetString(row, "STORE_KIND_NM_CHINA")) ?? string.Empty
            }).ToList();

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var inserted = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (inserted <= 0)
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }
    }
}
