using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class DepartmentInfoImportHandler : IExcelImportModuleHandler
    {
        private readonly IDepartmentInfoService _service;

        public DepartmentInfoImportHandler(IDepartmentInfoService service)
        {
            _service = service;
        }

        public string ModuleCd => "DepartmentInfo";

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var records = rows.Select(row => new DepartmentInfoRequest
            {
                DEPARTMENT_ID = 0,
                COMPANY_CD = companyCd,
                DEPARTMENT_CD = NormalizeRequiredText(Common.GetString(row, "DEPARTMENT_CD")),
                PARENT_CD = NormalizeNullableText(Common.GetString(row, "PARENT_CD")),
                DEP_NAME_KOR = NormalizeNullableText(Common.GetString(row, "DEP_NAME_KOR")),
                DEP_NAME_ENG = NormalizeNullableText(Common.GetString(row, "DEP_NAME_ENG")),
                DEP_NAME_VIET = NormalizeRequiredText(Common.GetString(row, "DEP_NAME_VIET")),
                DEP_NAME_CHINA = NormalizeNullableText(Common.GetString(row, "DEP_NAME_CHINA")),
                UPDATE_AT = now,
                UPDATE_BY = userId,
                CREATE_AT = now,
                CREATE_BY = userId,
                ISDEL = NormalizeFlagString(Common.GetString(row, "ISDEL"), "0")
            }).ToList();

            var inserted = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (inserted <= 0)
            {
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));
            }

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }
    }
}
