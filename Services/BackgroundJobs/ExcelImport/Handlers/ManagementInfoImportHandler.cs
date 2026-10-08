using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ManagementInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "ManagementInfo";
        private readonly IManagementInfoService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _managementIdsByCode;

        public ManagementInfoImportHandler(IManagementInfoService service, IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(IReadOnlyList<Dictionary<string, object>> rows, ExcelImportJobRequest request, List<ExcelImportResultRowDto> validateResults, CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);
            var importingCodes = rows
                .Select(row => Common.NormalizeNullableText(Common.GetString(row, "MG_CD")))
                .Where(code => code != null)
                .Select(code => code!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rootCd = Common.NormalizeNullableText(Common.GetString(rows[i], "MG_CD_ROOT"));
                if (rootCd != null && !_managementIdsByCode!.ContainsKey(rootCd) && !importingCodes.Contains(rootCd))
                {
                    validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i), "MG_CD_ROOT", rootCd, lang));
                }
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            var now = DateTime.Now;

            var requests = new List<ManagementInfoRequest>();

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];

                var mgCd = Common.NormalizeRequiredText(Common.GetString(row, "MG_CD"));

                var req = new ManagementInfoRequest
                {
                    MG_ID = 0,
                    COMPANY_CD = companyCd,
                    MG_CD = mgCd,
                    MG_DESC_KOR = Common.NormalizeNullableText(Common.GetString(row, "MG_DESC_KOR")),
                    MG_DESC_ENG = Common.NormalizeNullableText(Common.GetString(row, "MG_DESC_ENG")),
                    MG_DESC_VIET = Common.NormalizeNullableText(Common.GetString(row, "MG_DESC_VIET")),
                    MG_CD_ROOT = Common.NormalizeNullableText(Common.GetString(row, "MG_CD_ROOT")) ?? string.Empty,
                    ISDEL = Common.NormalizeFlagString(Common.GetString(row, "ISDEL"), "0"),
                    CREATE_AT = now,
                    UPDATE_AT = now,
                    CREATE_BY = userId,
                    UPDATE_BY = userId
                };

                requests.Add(req);

                var percent = 10 + (int)Math.Round((i + 1) * 20m / rows.Count);
                await progressWriter.ReportAsync(i + 1, rows.Count, percent, await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, rows.Count, lang), cancellationToken);
            }

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var inserted = await _service.BulkInsertAsync(companyCd, userId, requests, request.DatabaseName, lang);

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _managementIdsByCode ??= await _lookupRepository.GetManagementIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
