using System.Globalization;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    /// <summary>
    /// Imports material codes and quantities into the single product currently
    /// selected on /cost/material-product. The parent PRODUCT_ID is never read
    /// from Excel; it is validated against the authenticated company.
    /// </summary>
    public sealed class ProductMaterialInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "ProductMaterialInfo";

        private readonly DapperExecutor _db;
        private readonly IExcelImportLookupRepository _lookupRepository;

        public ProductMaterialInfoImportHandler(
            DapperExecutor db,
            IExcelImportLookupRepository lookupRepository)
        {
            _db = db;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var i18n = await ProductMaterialI18n.LoadAsync(ExcelImportHandlerHelper.GetRequestLanguage(request));
            if (!TryGetProductId(request, out var productId))
            {
                validateResults.Add(Error(0, i18n.T("PM_IMPORT_PARENT_REQUIRED")));
                return;
            }

            var productIds = await _lookupRepository.GetProductIdsByCodeAsync(
                request.CompanyCd, request.DatabaseName);

            if (!productIds.Values.Contains(productId))
            {
                validateResults.Add(Error(0, i18n.T("PM_IMPORT_PARENT_NOT_FOUND")));
                return;
            }

            var existing = await _db.QueryAsync<ProductMaterialInfoDto>(
                Net_DB.Net_DB_Company,
                "CALL sp_product_material_info_get(@p_COMPANY_CD, @p_PRODUCT_ID)",
                new { p_COMPANY_CD = request.CompanyCd, p_PRODUCT_ID = productId },
                request.DatabaseName);
            var alreadyDeclared = existing.Select(row => row.MATERIAL_PRODUCT_ID).ToHashSet();
            var seen = new HashSet<long>();

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var code = Common.NormalizeNullableText(Common.GetString(row, "MATERIAL_PRODUCT_CD"));

                if (code == null || !productIds.TryGetValue(code, out var materialId))
                {
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_MATERIAL_UNKNOWN", code ?? i18n.T("PM_IMPORT_EMPTY_CODE"))));
                    continue;
                }

                if (materialId == productId)
                {
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_SELF_REFERENCE")));
                }
                if (!seen.Add(materialId))
                {
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_DUPLICATE_MATERIAL", code)));
                }
                if (alreadyDeclared.Contains(materialId))
                {
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_ALREADY_EXISTS_SINGLE", code)));
                }

                if (!TryGetQuantity(row, out var quantity) || quantity <= 0m ||
                    quantity > 999999999999.999999m || decimal.Round(quantity, 6) != quantity)
                {
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_QUANTITY_INVALID")));
                }
            }
        }

        public async Task SaveAsync(
            List<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            IExcelImportJobProgressWriter progressWriter,
            CancellationToken cancellationToken)
        {
            var i18n = await ProductMaterialI18n.LoadAsync(ExcelImportHandlerHelper.GetRequestLanguage(request));
            if (!TryGetProductId(request, out var productId))
                throw new InvalidOperationException(i18n.T("PM_IMPORT_PARENT_INVALID"));

            var productIds = await _lookupRepository.GetProductIdsByCodeAsync(request.CompanyCd, request.DatabaseName);
            var userId = request.UserId ?? string.Empty;

            await progressWriter.ReportPercentAsync(35, i18n.T("PM_IMPORT_SAVE_SINGLE_START"), cancellationToken);
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, request.DatabaseName);

            // One transaction: a failure rolls back all rows of this file.
            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var code = Common.NormalizeRequiredText(Common.GetString(row, "MATERIAL_PRODUCT_CD"));
                if (!productIds.TryGetValue(code, out var materialId))
                    throw new InvalidOperationException(i18n.T("PM_IMPORT_ROW_ERROR", rowNo, i18n.T("PM_IMPORT_ROW_MATERIAL_NOT_FOUND")));

                if (!TryGetQuantity(row, out var quantity) || quantity <= 0m ||
                    quantity > 999999999999.999999m || decimal.Round(quantity, 6) != quantity)
                    throw new InvalidOperationException(i18n.T("PM_IMPORT_ROW_ERROR", rowNo, i18n.T("PM_IMPORT_ROW_QUANTITY_INVALID")));

                try
                {
                    await session.QuerySingleAsync<long>(
                        @"CALL sp_product_material_info_upsert(
                            @p_MATERIAL_ID, @p_COMPANY_CD, @p_PRODUCT_ID,
                            @p_MATERIAL_PRODUCT_ID, @p_QUANTITY, @p_CREATE_BY, @p_UPDATE_BY)",
                        new
                        {
                            p_MATERIAL_ID = 0,
                            p_COMPANY_CD = request.CompanyCd,
                            p_PRODUCT_ID = productId,
                            p_MATERIAL_PRODUCT_ID = materialId,
                            p_QUANTITY = quantity,
                            p_CREATE_BY = userId,
                            p_UPDATE_BY = userId
                        });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(i18n.T("PM_IMPORT_ROW_ERROR", rowNo, i18n.TranslateStoredProcedureError(ex.Message)), ex);
                }

                if ((i + 1) % 20 == 0 || i == rows.Count - 1)
                {
                    await progressWriter.ReportAsync(
                        i + 1, rows.Count,
                        35 + (int)((i + 1) * 60m / rows.Count),
                        i18n.T("PM_IMPORT_PROGRESS", i + 1, rows.Count), cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            session.Commit();
            await progressWriter.ReportPercentAsync(100, i18n.T("PM_IMPORT_SINGLE_DONE", rows.Count), cancellationToken);
        }

        private static bool TryGetQuantity(IReadOnlyDictionary<string, object> row, out decimal quantity)
        {
            quantity = 0m;
            if (!row.TryGetValue("QUANTITY", out var value) || value == null)
                return false;

            // Template cells are numeric, but users can paste "2.5" or "2,5".
            // Do not allow a thousands separator: it could turn 2,5 into 25.
            var raw = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().Replace(',', '.');
            return !string.IsNullOrEmpty(raw)
                && decimal.TryParse(
                    raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out quantity);
        }

        private static bool TryGetProductId(ExcelImportJobRequest request, out long productId)
        {
            productId = 0;
            return request.Params.TryGetValue("productId", out var raw)
                && long.TryParse(raw, out productId)
                && productId > 0;
        }

        private static ExcelImportResultRowDto Error(int rowNo, string message) => new()
        {
            RowNo = rowNo,
            Status = "ERROR",
            Message = message
        };
    }
}
