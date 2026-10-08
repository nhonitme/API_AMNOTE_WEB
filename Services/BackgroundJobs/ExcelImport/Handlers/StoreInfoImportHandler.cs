using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class StoreInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "StoreInfo";
        private readonly IStoreInfoService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _storeKindIdsByCode;

        public StoreInfoImportHandler(IStoreInfoService service, IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(IReadOnlyList<Dictionary<string, object>> rows, ExcelImportJobRequest request, List<ExcelImportResultRowDto> validateResults, CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureStoreKindMapAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i), "STORE_KIND_CD", Common.GetString(rows[i], "STORE_KIND_CD"), _storeKindIdsByCode!, lang);
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureStoreKindMapAsync(companyCd, request.DatabaseName);

            var records = new List<StoreInfoRequest>();

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];

                var storeCd = Common.NormalizeRequiredText(Common.GetString(row, "STORE_CD"));
                var storeName = Common.NormalizeRequiredText(Common.GetString(row, "STORE_NM_VIET"));
                var storeKindCd = Common.NormalizeNullableText(Common.GetString(row, "STORE_KIND_CD"));

                var storeKindId = 0;
                if (!string.IsNullOrWhiteSpace(storeKindCd) && _storeKindIdsByCode!.TryGetValue(storeKindCd!, out var id))
                    storeKindId = Convert.ToInt32(id);

                records.Add(new StoreInfoRequest
                {
                    STORE_ID = 0,
                    STORE_CD = storeCd,
                    STORE_NM_VIET = storeName,
                    STORE_NM_ENG = Common.NormalizeNullableText(Common.GetString(row, "STORE_NM_ENG")) ?? string.Empty,
                    STORE_NM_KOR = Common.NormalizeNullableText(Common.GetString(row, "STORE_NM_KOR")) ?? string.Empty,
                    STORE_NM_CHINA = Common.NormalizeNullableText(Common.GetString(row, "STORE_NM_CHINA")) ?? string.Empty,
                    STORE_KIND_ID = storeKindId
                });

                var percent = 10 + (int)Math.Round((i + 1) * 20m / rows.Count);
                await progressWriter.ReportAsync(i + 1, rows.Count, percent, await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, rows.Count, lang), cancellationToken);
            }

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var inserted = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (inserted <= 0)
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }

        private async Task EnsureStoreKindMapAsync(string companyCd, string? databaseName)
        {
            _storeKindIdsByCode ??= await _lookupRepository.GetStoreKindIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
