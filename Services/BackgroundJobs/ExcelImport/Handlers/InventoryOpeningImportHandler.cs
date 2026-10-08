using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class InventoryOpeningImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "InventoryOpening";

        private readonly IInventoryOpeningService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _productIdsByCode;
        private IReadOnlyDictionary<string, long>? _storeIdsByCode;
        private IReadOnlyDictionary<string, long>? _unitIdsByCode;

        public InventoryOpeningImportHandler(
            IInventoryOpeningService service,
            IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var productCd = Common.GetString(row, "PRODUCT_CD");
                var storeCd = Common.GetString(row, "STORE_CD");
                var unitCd = Common.GetString(row, "UNIT_CD");

                ExcelImportReferenceValidation.AddMissingCodeError(
                    validateResults, rowNo, "PRODUCT_CD", productCd, _productIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(
                    validateResults, rowNo, "STORE_CD", storeCd, _storeIdsByCode!, lang);

                if (!string.IsNullOrWhiteSpace(unitCd))
                {
                    ExcelImportReferenceValidation.AddMissingCodeError(
                        validateResults, rowNo, "UNIT_CD", unitCd, _unitIdsByCode!, lang);
                }
            }
            // PRODUCT|STORE uniqueness: InventoryOpeningService/Create → repository NormalizeRequestAsync only.
        }

        public async Task SaveAsync(
            List<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            IExcelImportJobProgressWriter progressWriter,
            CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);

            var records = new List<(int RowNo, InventoryOpeningRequest Request)>();
            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var productCd = Common.NormalizeRequiredText(Common.GetString(row, "PRODUCT_CD"));
                var storeCd = Common.NormalizeRequiredText(Common.GetString(row, "STORE_CD"));
                var unitCd = Common.NormalizeNullableText(Common.GetString(row, "UNIT_CD"));
                var quantity = Common.GetDecimal(row, "QUANTITY");
                var unitPrice = Common.GetDecimal(row, "UNIT_PRICE_CC");
                var amount = Common.GetDecimal(row, "AMOUNT_CC");
                if (amount == 0)
                {
                    amount = quantity * unitPrice;
                }

                records.Add((rowNo, new InventoryOpeningRequest
                {
                    PRODUCT_ID = ExcelImportReferenceValidation.ResolveId(productCd, _productIdsByCode!),
                    PRODUCT_CD = productCd,
                    STORE_ID = ExcelImportReferenceValidation.ResolveId(storeCd, _storeIdsByCode!),
                    STORE_CD = storeCd,
                    UNIT_ID = string.IsNullOrWhiteSpace(unitCd)
                        ? null
                        : ExcelImportReferenceValidation.ResolveId(unitCd, _unitIdsByCode!),
                    UNIT_CD = unitCd,
                    QUANTITY = quantity,
                    UNIT_PRICE_CC = unitPrice,
                    AMOUNT_CC = amount,
                    SUMMARY = Common.NormalizeNullableText(Common.GetString(row, "SUMMARY"))
                }));

                var percent = 10 + (int)Math.Round((i + 1) * 20m / rows.Count);
                await progressWriter.ReportAsync(
                    i + 1,
                    rows.Count,
                    percent,
                    await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, rows.Count, lang),
                    cancellationToken);
            }

            await progressWriter.ReportPercentAsync(
                35,
                await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang),
                cancellationToken);

            var inserted = 0;
            foreach (var (rowNo, record) in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _service.CreateAsync(companyCd, userId, record);
                    inserted++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Row {rowNo}: {ex.Message}", ex);
                }
            }

            if (inserted <= 0)
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));

            await progressWriter.ReportPercentAsync(
                100,
                await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang),
                cancellationToken);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _productIdsByCode ??= await _lookupRepository.GetProductIdsByCodeAsync(companyCd, databaseName);
            _storeIdsByCode ??= await _lookupRepository.GetStoreIdsByCodeAsync(companyCd, databaseName);
            _unitIdsByCode ??= await _lookupRepository.GetProductUnitIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
