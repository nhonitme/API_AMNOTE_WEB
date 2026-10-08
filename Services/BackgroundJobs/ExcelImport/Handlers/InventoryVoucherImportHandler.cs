using System.Globalization;
using System.Reflection;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class InventoryVoucherImportHandler : IExcelImportModuleHandler, IExcelImportModuleAliasProvider
    {
        private static readonly Dictionary<string, InventoryImportModule> Modules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["InventoryReceiptVoucherAp"] = new("AP", "IR"),
            ["InventoryIssueVoucherAr"] = new("AR", "IO"),
            ["InventoryAdjustVoucherInv"] = new("INV", "IA")
        };

        private readonly IInventoryVoucherWriteService _writeService;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _productIdsByCode;
        private IReadOnlyDictionary<string, long>? _storeIdsByCode;
        private IReadOnlyDictionary<string, long>? _unitIdsByCode;

        public InventoryVoucherImportHandler(IInventoryVoucherWriteService writeService, IExcelImportLookupRepository lookupRepository)
        {
            _writeService = writeService;
            _lookupRepository = lookupRepository;
        }

        public string ModuleCd => "InventoryVoucher";
        public IReadOnlyCollection<string> ModuleCds => Modules.Keys.ToList();

        public async Task ValidateRowsAsync(IReadOnlyList<Dictionary<string, object>> rows, ExcelImportJobRequest request, List<ExcelImportResultRowDto> validateResults, CancellationToken cancellationToken)
        {
            var lang = request.Lang ?? Common.GetCurrentLanguage();
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);

                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "PRODUCT_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_PRODUCT_CD", "INPUT_PRODUCT_CD", "OUTPUT_PRODUCT_CD", "PRODUCT_CD"), _productIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "STORE_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_STORE_CD", "INPUT_STORE_CD", "OUTPUT_STORE_CD", "STORE_CD"), _storeIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "UNIT_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_UNIT_CD", "INPUT_UNIT_CD", "OUTPUT_UNIT_CD", "UNIT_CD"), _unitIdsByCode!, lang);
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var lang = request.Lang ?? Common.GetCurrentLanguage();
            var module = ResolveModule(request.ModuleCd, lang);
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);
            var templateKeys = NormalizeTemplateKeys((await Common.GetExcelTemplateKeysAsync(request.ModuleCd, companyCd)).Keys);
            var recordsResult = await BuildInventoryImportRecordsAsync(rows, templateKeys, companyCd, module.InputType, module.ChitType, lang);
            var errors = new List<string>(recordsResult.Errors);

            errors = ChitNoteExcelHelper.NormalizeImportErrors(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            var orderedRecords = recordsResult.Records.OrderBy(item => item.Row).ToList();
            var codeMaps = new InventoryVoucherCodeMaps
            {
                ProductIdsByCode = _productIdsByCode,
                StoreIdsByCode = _storeIdsByCode,
                UnitIdsByCode = _unitIdsByCode
            };
            for (var i = 0; i < orderedRecords.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _writeService.CreateAsync(
                    companyCd,
                    module.InputType,
                    module.ChitType,
                    userId,
                    orderedRecords[i].Record,
                    codeMaps,
                    request.DatabaseName,
                    lang);
                await progressWriter.ReportAsync(i + 1, orderedRecords.Count, 40 + (int)Math.Round((i + 1) * 60m / orderedRecords.Count), await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, orderedRecords.Count, lang), cancellationToken);
            }
        }

        private static InventoryImportModule ResolveModule(string moduleCd, string lang)
        {
            if (Modules.TryGetValue(moduleCd, out var module))
            {
                return module;
            }

            throw new InvalidOperationException(ExcelImportHandlerHelper.GetUnsupportedModuleMessage(moduleCd, lang));
        }

        private static async Task<(List<(InventoryVoucherRequest Record, int Row)> Records, List<string> Errors)> BuildInventoryImportRecordsAsync(
            List<Dictionary<string, object>> importedData,
            IReadOnlyList<string> templateKeys,
            string companyCd,
            string inputType,
            string chitType,
            string lang)
        {
            var records = new Dictionary<string, (InventoryVoucherRequest Record, int Row)>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            var rowNumber = 2;

            foreach (var row in importedData)
            {
                var groupKey = GetInventoryImportGroupKey(row, rowNumber);
                if (!records.TryGetValue(groupKey, out var item))
                {
                    item = (new InventoryVoucherRequest
                    {
                        COMPANY_CD = companyCd,
                        INPUT_TYPE = inputType,
                        CHIT_TYPE = chitType,
                        ISEXCEL = "1",
                        ISDEL = "0",
                        IS_LOCK = "0",
                        IS_CONFIRMED = "0",
                        IS_PAYMENT = "0"
                    }, rowNumber);
                    records[groupKey] = item;
                }

                MergeInventoryHeaderValues(item.Record, row, templateKeys);
                item.Record.COMPANY_CD = companyCd;
                item.Record.INPUT_TYPE = inputType;
                item.Record.CHIT_TYPE = chitType;
                item.Record.ISEXCEL = "1";

                if (chitType == "IR")
                {
                    var input = BuildInventoryInputFromRow(row, templateKeys, companyCd, chitType, item.Record.CHIT_YMD, item.Record.INPUTS.Count + 1);
                    if (input != null)
                    {
                        item.Record.INPUTS.Add(input);
                    }
                }
                else if (chitType == "IO")
                {
                    var output = BuildInventoryOutputFromRow(row, templateKeys, companyCd, chitType, item.Record.CHIT_YMD, item.Record.OUTPUTS.Count + 1);
                    if (output != null)
                    {
                        item.Record.OUTPUTS.Add(output);
                    }
                }
                else
                {
                    var input = BuildInventoryInputFromRow(row, templateKeys, companyCd, chitType, item.Record.CHIT_YMD, item.Record.INPUTS.Count + 1);
                    if (input != null)
                    {
                        item.Record.INPUTS.Add(input);
                    }

                    var output = BuildInventoryOutputFromRow(row, templateKeys, companyCd, chitType, item.Record.CHIT_YMD, item.Record.OUTPUTS.Count + 1);
                    if (output != null)
                    {
                        item.Record.OUTPUTS.Add(output);
                    }
                }

                rowNumber++;
            }

            foreach (var item in records.Values)
            {
                var hasLines = chitType switch
                {
                    "IR" => item.Record.INPUTS.Count > 0,
                    "IO" => item.Record.OUTPUTS.Count > 0,
                    "IA" => item.Record.INPUTS.Count > 0 && item.Record.OUTPUTS.Count > 0,
                    _ => false
                };

                if (!hasLines)
                {
                    errors.Add($"Row {item.Row}: {(await Common.getLanguage("DETAIL_INFO", lang))} {(await Common.getLanguage("REQUIRED", lang))}");
                }
            }

            return (records.Values.ToList(), errors);
        }

        private static string GetInventoryImportGroupKey(IDictionary<string, object> row, int rowNumber)
        {
            var chitNo = Common.GetStringValue(row, "CHIT_NO");
            if (!string.IsNullOrWhiteSpace(chitNo))
            {
                return $"NO:{chitNo.Trim()}";
            }

            var chitCd = Common.GetStringValue(row, "CHIT_CD");
            if (!string.IsNullOrWhiteSpace(chitCd))
            {
                return $"CD:{chitCd.Trim()}";
            }

            return $"ROW:{rowNumber}";
        }

        private static void MergeInventoryHeaderValues(InventoryVoucherRequest record, IDictionary<string, object> row, IReadOnlyList<string> templateKeys)
        {
            foreach (var key in templateKeys)
            {
                if (IsInventoryDetailTemplateKey(key))
                {
                    continue;
                }

                var property = GetPublicProperty(typeof(InventoryVoucherRequest), key);
                if (property == null || !property.CanWrite || property.Name is nameof(InventoryVoucherRequest.INPUTS) or nameof(InventoryVoucherRequest.OUTPUTS))
                {
                    continue;
                }

                var value = GetExcelCellValue(row, key, property.PropertyType);
                SetPropertyValueIfEmpty(record, property, value);
            }
        }

        private static InventoryInput? BuildInventoryInputFromRow(IDictionary<string, object> row, IReadOnlyList<string> templateKeys, string companyCd, string chitType, string? headerYmd, int sortOrder)
        {
            var input = new InventoryInput
            {
                COMPANY_CD = companyCd,
                CHIT_TYPE = chitType,
                FC_TYPE = "VND",
                INVENTORY_YMD = headerYmd,
                STATE = "1",
                ISDEL = "0",
                SORT = sortOrder
            };

            if (!ApplyInventoryDetailValues(input, typeof(InventoryInput), row, templateKeys))
            {
                return null;
            }

            input.INVENTORY_YMD = NormalizeNullableText(input.INVENTORY_YMD) ?? headerYmd;
            if (input.AMOUNT_CC == 0 && input.QUANTITY != 0 && input.UNIT_PRICE_CC != 0)
            {
                input.AMOUNT_CC = input.QUANTITY * input.UNIT_PRICE_CC;
            }

            return input;
        }

        private static InventoryOutput? BuildInventoryOutputFromRow(IDictionary<string, object> row, IReadOnlyList<string> templateKeys, string companyCd, string chitType, string? headerYmd, int sortOrder)
        {
            var output = new InventoryOutput
            {
                COMPANY_CD = companyCd,
                CHIT_TYPE = chitType,
                FC_TYPE = "VND",
                INVENTORY_YMD = headerYmd,
                STATE = "1",
                ISDEL = "0",
                SORT = sortOrder
            };

            if (!ApplyInventoryDetailValues(output, typeof(InventoryOutput), row, templateKeys))
            {
                return null;
            }

            output.INVENTORY_YMD = NormalizeNullableText(output.INVENTORY_YMD) ?? headerYmd;
            if (output.AMOUNT_CC == 0 && output.QUANTITY != 0 && output.UNIT_PRICE_CC != 0)
            {
                output.AMOUNT_CC = output.QUANTITY * output.UNIT_PRICE_CC;
            }

            return output;
        }

        private static bool ApplyInventoryDetailValues(object target, Type detailType, IDictionary<string, object> row, IReadOnlyList<string> templateKeys)
        {
            var hasValue = false;

            foreach (var key in templateKeys)
            {
                var propertyName = Common.ResolveInventoryDetailPropertyName(key, detailType, IsInventoryDetailTemplateKey(key));
                if (propertyName == null)
                {
                    continue;
                }

                var property = GetPublicProperty(detailType, propertyName);
                if (property == null || !property.CanWrite)
                {
                    continue;
                }

                var value = GetExcelCellValue(row, key, property.PropertyType);
                if (IsEmptyExcelValue(value))
                {
                    continue;
                }

                property.SetValue(target, value);
                hasValue = true;
            }

            return hasValue;
        }

        private static bool IsInventoryDetailTemplateKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                   (key.StartsWith("DETAIL_", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("INPUT_", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("OUTPUT_", StringComparison.OrdinalIgnoreCase));
        }

        private static object? GetExcelCellValue(IDictionary<string, object> row, string key, Type targetType)
        {
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var actualType = nullableType ?? targetType;

            if (actualType == typeof(string))
            {
                if (key.EndsWith("_YMD", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_VMD", StringComparison.OrdinalIgnoreCase))
                {
                    return Common.GetYmdStringValue(row, key);
                }

                return Common.GetStringValue(row, key);
            }

            if (actualType == typeof(DateTime))
            {
                return Common.GetDateValue(row, key);
            }

            if (actualType == typeof(int))
            {
                return Common.GetNullableIntValue(row, key);
            }

            if (actualType == typeof(long))
            {
                return GetNullableLongValue(row, key);
            }

            if (actualType == typeof(decimal))
            {
                return Common.GetNullableDecimalValue(row, key);
            }

            if (actualType == typeof(bool))
            {
                var text = Common.GetStringValue(row, key);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (bool.TryParse(text, out var boolValue))
                {
                    return boolValue;
                }

                return text == "1";
            }

            if (!row.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                return null;
            }

            try
            {
                return Convert.ChangeType(rawValue, actualType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return rawValue;
            }
        }

        private static long? GetNullableLongValue(IDictionary<string, object> row, string key)
        {
            var longValue = Common.GetLongValue(row, key);
            if (longValue.HasValue)
            {
                return longValue;
            }

            var decimalValue = Common.GetNullableDecimalValue(row, key);
            return decimalValue.HasValue ? Convert.ToInt64(decimalValue.Value) : null;
        }

        private static bool IsEmptyExcelValue(object? value)
        {
            return value == null || value is string text && string.IsNullOrWhiteSpace(text);
        }

        private static void SetPropertyValueIfEmpty(object target, PropertyInfo property, object? nextValue)
        {
            if (IsEmptyExcelValue(nextValue))
            {
                return;
            }

            var currentValue = property.GetValue(target);
            if (property.PropertyType == typeof(string))
            {
                if (string.IsNullOrWhiteSpace(currentValue as string))
                {
                    property.SetValue(target, nextValue);
                }

                return;
            }

            if (currentValue == null)
            {
                property.SetValue(target, nextValue);
            }
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _productIdsByCode ??= await _lookupRepository.GetProductIdsByCodeAsync(companyCd, databaseName);
            _storeIdsByCode ??= await _lookupRepository.GetStoreIdsByCodeAsync(companyCd, databaseName);
            _unitIdsByCode ??= await _lookupRepository.GetProductUnitIdsByCodeAsync(companyCd, databaseName);
        }

        private sealed record InventoryImportModule(string InputType, string ChitType);
    }
}
