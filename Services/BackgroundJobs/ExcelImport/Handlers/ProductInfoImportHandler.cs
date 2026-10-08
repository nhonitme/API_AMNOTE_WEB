using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ProductInfoImportHandler : IExcelImportModuleHandler
    {
        private readonly IProductInfoService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _productKindIdsByCode;
        private IReadOnlyDictionary<string, long>? _productUnitIdsByCode;
        private IReadOnlyDictionary<string, long>? _storeIdsByCode;

        public ProductInfoImportHandler(IProductInfoService service, IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }

        public string ModuleCd => "ProductInfo";

        public async Task ValidateRowsAsync(IReadOnlyList<Dictionary<string, object>> rows, ExcelImportJobRequest request, List<ExcelImportResultRowDto> validateResults, CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);
                var row = rows[i];

                ExcelImportReferenceValidation.SoftOptionalCode(
                    validateResults, rows, i, rowNo, "PRODUCT_KIND_CD", _productKindIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(
                    validateResults, rowNo, "UNIT_CD", Common.GetString(row, "UNIT_CD"), _productUnitIdsByCode!, lang);
                ExcelImportReferenceValidation.SoftOptionalCode(
                    validateResults, rows, i, rowNo, "STORE_CD", _storeIdsByCode!, lang);
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);

            var records = rows.Select(row => new ProductInfoDto
            {
                COMPANY_CD = companyCd,
                PRODUCT_CD = Common.NormalizeRequiredText(Common.GetString(row, "PRODUCT_CD")),
                PRODUCT_NM_VIET = Common.NormalizeRequiredText(Common.GetString(row, "PRODUCT_NM_VIET")),
                PRODUCT_NM_ENG = Common.NormalizeNullableText(Common.GetString(row, "PRODUCT_NM_ENG")),
                PRODUCT_NM_KOR = Common.NormalizeNullableText(Common.GetString(row, "PRODUCT_NM_KOR")),
                PRODUCT_NM_CHINA = Common.NormalizeNullableText(Common.GetString(row, "PRODUCT_NM_CHINA")),
                PRODUCT_KIND_ID = ResolveIntId(Common.GetString(row, "PRODUCT_KIND_CD"), _productKindIdsByCode!),
                UNIT_ID = ResolveIntId(Common.GetString(row, "UNIT_CD"), _productUnitIdsByCode!),
                STORE_ID = ResolveIntId(Common.GetString(row, "STORE_CD"), _storeIdsByCode!),
                DIVISION = Common.NormalizeNullableText(Common.GetString(row, "DIVISION")),
                SUMMARY = Common.NormalizeNullableText(Common.GetString(row, "SUMMARY")),
                ISDEL = Common.NormalizeFlagString(Common.GetString(row, "ISDEL"), "0")
            }).ToList();

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var result = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (result <= 0)
            {
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));
            }

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(result, rows.Count, lang), cancellationToken);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _productKindIdsByCode ??= await _lookupRepository.GetProductKindIdsByCodeAsync(companyCd, databaseName);
            _productUnitIdsByCode ??= await _lookupRepository.GetProductUnitIdsByCodeAsync(companyCd, databaseName);
            _storeIdsByCode ??= await _lookupRepository.GetStoreIdsByCodeAsync(companyCd, databaseName);
        }

        private static int? ResolveIntId(string? fieldValue, IReadOnlyDictionary<string, long> lookup)
        {
            var id = ExcelImportReferenceValidation.ResolveId(fieldValue, lookup);
            return id.HasValue ? Convert.ToInt32(id.Value) : null;
        }
    }
}
