using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ProductKindImportHandler : IExcelImportModuleHandler
    {
        private readonly IProductKindService _service;

        public ProductKindImportHandler(IProductKindService service)
        {
            _service = service;
        }

        public string ModuleCd => "ProductKind";

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            var records = rows.Select(row => new ProductKind
            {
                COMPANY_CD = companyCd,
                PRODUCT_KIND_CD = Common.NormalizeRequiredText(Common.GetString(row, "PRODUCT_KIND_CD")),
                PRODUCTKIND_NM_VIET = Common.NormalizeRequiredText(Common.GetString(row, "PRODUCTKIND_NM_VIET")),
                PRODUCTKIND_NM_ENG = Common.NormalizeNullableText(Common.GetString(row, "PRODUCTKIND_NM_ENG")),
                PRODUCTKIND_NM_KOR = Common.NormalizeNullableText(Common.GetString(row, "PRODUCTKIND_NM_KOR")),
                PRODUCTKIND_NM_CHINA = Common.NormalizeNullableText(Common.GetString(row, "PRODUCTKIND_NM_CHINA")),
                REMARK = Common.NormalizeNullableText(Common.GetString(row, "REMARK")),
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
