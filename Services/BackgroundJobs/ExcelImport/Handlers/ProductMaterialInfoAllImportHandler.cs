using System.Globalization;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    /// <summary>
    /// Shared Excel job pipeline: each row supplies a finished PRODUCT_CD,
    /// MATERIAL_PRODUCT_CD and QUANTITY. All product IDs are looked up in the
    /// authenticated company; the import is atomic across all finished products.
    /// </summary>
    public sealed class ProductMaterialInfoAllImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "ProductMaterialInfoAll";

        private readonly DapperExecutor _db;
        private readonly IExcelImportLookupRepository _lookupRepository;

        public ProductMaterialInfoAllImportHandler(
            DapperExecutor db,
            IExcelImportLookupRepository lookupRepository)
        {
            _db = db;
            _lookupRepository = lookupRepository;
        }

        private sealed class MaterialPair
        {
            public long PRODUCT_ID { get; set; }
            public long MATERIAL_PRODUCT_ID { get; set; }
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var i18n = await ProductMaterialI18n.LoadAsync(ExcelImportHandlerHelper.GetRequestLanguage(request));
            var idsByCode = await _lookupRepository.GetProductIdsByCodeAsync(
                request.CompanyCd, request.DatabaseName);

            // Resolve all valid finished-product IDs before reading declared pairs.
            var finishedIds = rows
                .Select(row => NormalizeCode(Common.GetString(row, "PRODUCT_CD")))
                .Where(code => code.Length > 0)
                .Select(code => idsByCode.TryGetValue(code, out var id) ? id : 0L)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            var alreadyDeclared = new HashSet<(long ProductId, long MaterialId)>();
            if (finishedIds.Length > 0)
            {
                var existing = await _db.QueryAsync<MaterialPair>(
                    Net_DB.Net_DB_Company,
                    @"SELECT PRODUCT_ID, MATERIAL_PRODUCT_ID
                      FROM product_material_info
                      WHERE COMPANY_CD = @p_COMPANY_CD
                        AND IFNULL(ISDEL, '0') = '0'
                        AND PRODUCT_ID IN @p_PRODUCT_IDS",
                    new { p_COMPANY_CD = request.CompanyCd, p_PRODUCT_IDS = finishedIds },
                    request.DatabaseName);

                foreach (var item in existing)
                    alreadyDeclared.Add((item.PRODUCT_ID, item.MATERIAL_PRODUCT_ID));
            }

            var seen = new HashSet<(long ProductId, long MaterialId)>();
            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var productCode = NormalizeCode(Common.GetString(row, "PRODUCT_CD"));
                var materialCode = NormalizeCode(Common.GetString(row, "MATERIAL_PRODUCT_CD"));

                var productValid = idsByCode.TryGetValue(productCode, out var productId);
                var materialValid = idsByCode.TryGetValue(materialCode, out var materialId);

                if (!productValid)
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_PRODUCT_UNKNOWN", productCode)));
                if (!materialValid)
                    validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_MATERIAL_UNKNOWN", materialCode)));

                if (productValid && materialValid)
                {
                    if (productId == materialId)
                        validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_SELF_REFERENCE")));

                    var pair = (ProductId: productId, MaterialId: materialId);
                    if (!seen.Add(pair))
                        validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_DUPLICATE_PAIR", productCode, materialCode)));
                    if (alreadyDeclared.Contains(pair))
                        validateResults.Add(Error(rowNo, i18n.T("PM_IMPORT_ALREADY_EXISTS_PAIR", productCode, materialCode)));
                }

                if (!TryGetQuantity(row, out var quantity) ||
                    quantity <= 0m ||
                    quantity > 999999999999.999999m ||
                    decimal.Round(quantity, 6) != quantity)
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
            var idsByCode = await _lookupRepository.GetProductIdsByCodeAsync(
                request.CompanyCd, request.DatabaseName);
            var userId = request.UserId ?? string.Empty;

            await progressWriter.ReportPercentAsync(
                35, i18n.T("PM_IMPORT_SAVE_ALL_START"), cancellationToken);

            await using var session = await _db.CreateSessionAsync(
                Net_DB.Net_DB_Company, request.DatabaseName);

            try
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = rows[i];
                    var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                    var productCode = NormalizeCode(Common.GetString(row, "PRODUCT_CD"));
                    var materialCode = NormalizeCode(Common.GetString(row, "MATERIAL_PRODUCT_CD"));

                    if (!idsByCode.TryGetValue(productCode, out var productId) ||
                        !idsByCode.TryGetValue(materialCode, out var materialId))
                        throw new InvalidOperationException(i18n.T("PM_IMPORT_ROW_ERROR", rowNo, i18n.T("PM_IMPORT_ROW_CODES_NOT_FOUND")));

                    if (productId == materialId || !TryGetQuantity(row, out var quantity) ||
                        quantity <= 0m || quantity > 999999999999.999999m ||
                        decimal.Round(quantity, 6) != quantity)
                        throw new InvalidOperationException(i18n.T("PM_IMPORT_ROW_ERROR", rowNo, i18n.T("PM_IMPORT_ROW_DATA_INVALID")));

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

                    if ((i + 1) % 25 == 0 || i == rows.Count - 1)
                    {
                        await progressWriter.ReportAsync(
                            i + 1, rows.Count,
                            35 + (int)((i + 1) * 60m / rows.Count),
                            i18n.T("PM_IMPORT_PROGRESS", i + 1, rows.Count), cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }

            await progressWriter.ReportPercentAsync(
                100, i18n.T("PM_IMPORT_ALL_DONE", rows.Count), cancellationToken);
        }

        private static string NormalizeCode(string? code)
            => (Common.NormalizeNullableText(code) ?? string.Empty).Trim();

        private static bool TryGetQuantity(IReadOnlyDictionary<string, object> row, out decimal quantity)
        {
            quantity = 0m;
            if (!row.TryGetValue("QUANTITY", out var value) || value == null)
                return false;

            // Accept 2.5 and 2,5 but reject thousands separators to avoid ambiguity.
            var raw = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().Replace(',', '.');
            return !string.IsNullOrWhiteSpace(raw)
                && decimal.TryParse(
                    raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out quantity);
        }

        private static ExcelImportResultRowDto Error(int rowNo, string message) => new()
        {
            RowNo = rowNo,
            Status = "ERROR",
            Message = message
        };
    }
}
