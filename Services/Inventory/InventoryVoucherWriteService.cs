using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.Inventory
{
    public sealed class InventoryVoucherWriteService : IInventoryVoucherWriteService
    {
        private readonly IInventoryVoucherRepository _repository;

        public InventoryVoucherWriteService(IInventoryVoucherRepository repository)
        {
            _repository = repository;
        }

        public Task<long> CreateAsync(
            string companyCd,
            string inputType,
            string chitType,
            string userId,
            InventoryVoucherRequest request,
            InventoryVoucherCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null)
        {
            var normalized = BuildCreateRequest(request, companyCd, inputType, chitType, codeMaps, lang);
            return _repository.SaveInventoryVoucherAsync(companyCd, inputType, userId, normalized, databaseName);
        }

        public Task<long> UpdateAsync(
            string companyCd,
            string inputType,
            string chitType,
            string userId,
            InventoryVoucherDto existing,
            InventoryVoucherRequest request,
            InventoryVoucherCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null)
        {
            var normalized = BuildUpdateRequest(existing, request, companyCd, inputType, chitType, codeMaps, lang);
            return _repository.SaveInventoryVoucherAsync(companyCd, inputType, userId, normalized, databaseName);
        }

        private static InventoryVoucherRequest BuildCreateRequest(
            InventoryVoucherRequest request,
            string companyCd,
            string inputType,
            string chitType,
            InventoryVoucherCodeMaps? codeMaps,
            string? lang)
        {
            var headerDate = NormalizeNullableYmdText(request.CHIT_YMD, nameof(InventoryVoucherRequest.CHIT_YMD));
            var inputs = NormalizeInputs(request.INPUTS, companyCd, headerDate, chitType, codeMaps);
            var outputs = NormalizeOutputs(request.OUTPUTS, companyCd, headerDate, chitType, codeMaps);
            ValidateInventoryLines(chitType, inputs, outputs, lang);

            return new InventoryVoucherRequest
            {
                CHIT_ID = 0,
                COMPANY_CD = companyCd,
                INPUT_TYPE = inputType,
                CHIT_CD = null,
                CHIT_NO = NormalizeNullableText(request.CHIT_NO),
                CHIT_YMD = headerDate,
                CHIT_TYPE = chitType,
                AMOUNT = request.AMOUNT,
                TOTAL_QTY = request.TOTAL_QTY,
                REMARK = NormalizeNullableText(request.REMARK) ?? NormalizeNullableText(request.NOTE),
                PAYER_INFO = NormalizeNullableText(request.PAYER_INFO),
                ISDEL = "0",
                IS_LOCK = NormalizeFlagString(request.IS_LOCK, "0"),
                ISEXCEL = NormalizeFlagString(request.ISEXCEL, "0"),
                EMAIL_EPAY = NormalizeNullableText(request.EMAIL_EPAY),
                IS_CONFIRMED = NormalizeFlagString(request.IS_CONFIRMED, "0"),
                NOTE = NormalizeNullableText(request.NOTE),
                DAY_OF_PAYMENT = NormalizeNullablePositiveInt(request.DAY_OF_PAYMENT),
                TIME_FOR_PAYMENT = NormalizeNullableText(request.TIME_FOR_PAYMENT),
                IS_PAYMENT = NormalizeFlagString(request.IS_PAYMENT, "0"),
                CHIT_CD_COGS = NormalizeNullableText(request.CHIT_CD_COGS),
                DESCRIPTION_VIET = NormalizeNullableText(request.DESCRIPTION_VIET),
                DESCRIPTION_ENG = NormalizeNullableText(request.DESCRIPTION_ENG),
                DESCRIPTION_KOR = NormalizeNullableText(request.DESCRIPTION_KOR),
                INPUTS = inputs,
                OUTPUTS = outputs
            };
        }

        private static InventoryVoucherRequest BuildUpdateRequest(
            InventoryVoucherDto existing,
            InventoryVoucherRequest request,
            string companyCd,
            string inputType,
            string chitType,
            InventoryVoucherCodeMaps? codeMaps,
            string? lang)
        {
            var headerDate = request.CHIT_YMD == null
                ? existing.CHIT_YMD
                : NormalizeNullableYmdText(request.CHIT_YMD, nameof(InventoryVoucherRequest.CHIT_YMD));
            var inputs = NormalizeInputs(request.INPUTS, companyCd, headerDate, chitType, codeMaps);
            var outputs = NormalizeOutputs(request.OUTPUTS, companyCd, headerDate, chitType, codeMaps);
            ValidateInventoryLines(chitType, inputs, outputs, lang);

            return new InventoryVoucherRequest
            {
                CHIT_ID = existing.CHIT_ID,
                COMPANY_CD = companyCd,
                INPUT_TYPE = inputType,
                CHIT_CD = existing.CHIT_CD,
                CHIT_NO = request.CHIT_NO == null ? NormalizeNullableText(existing.CHIT_NO) : NormalizeNullableText(request.CHIT_NO),
                CHIT_YMD = headerDate,
                CHIT_TYPE = chitType,
                AMOUNT = request.AMOUNT,
                TOTAL_QTY = request.TOTAL_QTY,
                REMARK = request.REMARK == null ? NormalizeNullableText(existing.REMARK ?? existing.NOTE) : NormalizeNullableText(request.REMARK),
                PAYER_INFO = request.PAYER_INFO == null ? NormalizeNullableText(existing.PAYER_INFO) : NormalizeNullableText(request.PAYER_INFO),
                ISDEL = "0",
                IS_LOCK = request.IS_LOCK == null ? NormalizeFlagString(existing.IS_LOCK, "0") : NormalizeFlagString(request.IS_LOCK, "0"),
                ISEXCEL = request.ISEXCEL == null ? NormalizeFlagString(existing.ISEXCEL, "0") : NormalizeFlagString(request.ISEXCEL, "0"),
                EMAIL_EPAY = request.EMAIL_EPAY == null ? NormalizeNullableText(existing.EMAIL_EPAY) : NormalizeNullableText(request.EMAIL_EPAY),
                IS_CONFIRMED = request.IS_CONFIRMED == null ? NormalizeFlagString(existing.IS_CONFIRMED, "0") : NormalizeFlagString(request.IS_CONFIRMED, "0"),
                NOTE = request.NOTE == null ? NormalizeNullableText(existing.NOTE) : NormalizeNullableText(request.NOTE),
                DAY_OF_PAYMENT = request.DAY_OF_PAYMENT.HasValue ? NormalizeNullablePositiveInt(request.DAY_OF_PAYMENT) : NormalizeNullablePositiveInt(existing.DAY_OF_PAYMENT),
                TIME_FOR_PAYMENT = request.TIME_FOR_PAYMENT == null ? NormalizeNullableText(existing.TIME_FOR_PAYMENT) : NormalizeNullableText(request.TIME_FOR_PAYMENT),
                IS_PAYMENT = request.IS_PAYMENT == null ? NormalizeFlagString(existing.IS_PAYMENT, "0") : NormalizeFlagString(request.IS_PAYMENT, "0"),
                CHIT_CD_COGS = request.CHIT_CD_COGS == null ? NormalizeNullableText(existing.CHIT_CD_COGS) : NormalizeNullableText(request.CHIT_CD_COGS),
                DESCRIPTION_VIET = request.DESCRIPTION_VIET == null ? NormalizeNullableText(existing.DESCRIPTION_VIET) : NormalizeNullableText(request.DESCRIPTION_VIET),
                DESCRIPTION_ENG = request.DESCRIPTION_ENG == null ? NormalizeNullableText(existing.DESCRIPTION_ENG) : NormalizeNullableText(request.DESCRIPTION_ENG),
                DESCRIPTION_KOR = request.DESCRIPTION_KOR == null ? NormalizeNullableText(existing.DESCRIPTION_KOR) : NormalizeNullableText(request.DESCRIPTION_KOR),
                INPUTS = inputs,
                OUTPUTS = outputs
            };
        }

        private static List<InventoryInput> NormalizeInputs(
            IEnumerable<InventoryInput>? inputs,
            string companyCd,
            string? headerDate,
            string chitType,
            InventoryVoucherCodeMaps? codeMaps)
        {
            if (!UsesInventoryInput(chitType))
            {
                return new List<InventoryInput>();
            }

            return (inputs ?? Enumerable.Empty<InventoryInput>())
                .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                .Select((item, index) => new InventoryInput
                {
                    INPUT_ID = Common.NormalizeNullablePositiveLong(item.INPUT_ID),
                    INPUT_CD = NormalizeNullableText(item.INPUT_CD) ?? string.Empty,
                    INVENTORY_ID = Common.NormalizeNullablePositiveLong(item.INVENTORY_ID),
                    INVENTORY_CD = NormalizeNullableText(item.INVENTORY_CD),
                    CHIT_TYPE = chitType,
                    COMPANY_CD = companyCd,
                    PRODUCT_ID = ResolveCodeId(item.PRODUCT_CD, codeMaps?.ProductIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.PRODUCT_ID),
                    PRODUCT_CD = NormalizeNullableText(item.PRODUCT_CD) ?? string.Empty,
                    STORE_ID = ResolveCodeId(item.STORE_CD, codeMaps?.StoreIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.STORE_ID),
                    STORE_CD = NormalizeNullableText(item.STORE_CD) ?? string.Empty,
                    UNIT_ID = ResolveCodeId(item.UNIT_CD, codeMaps?.UnitIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.UNIT_ID),
                    UNIT_CD = NormalizeNullableText(item.UNIT_CD) ?? string.Empty,
                    QUANTITY = item.QUANTITY,
                    UNIT_PRICE_CC = item.UNIT_PRICE_CC,
                    FC_TYPE = NormalizeNullableText(item.FC_TYPE) ?? "VND",
                    UNIT_PRICE_FC = item.UNIT_PRICE_FC,
                    EXCHANGE_RATES = item.EXCHANGE_RATES,
                    AMOUNT_CC = item.AMOUNT_CC != 0 ? item.AMOUNT_CC : item.QUANTITY * item.UNIT_PRICE_CC,
                    AMOUNT_FC = item.AMOUNT_FC,
                    SUMMARY = NormalizeNullableText(item.SUMMARY) ?? string.Empty,
                    INVENTORY_YMD = NormalizeNullableInventoryDateTimeText(item.INVENTORY_YMD, nameof(InventoryInput.INVENTORY_YMD)) ?? headerDate,
                    STATE = NormalizeNullableText(item.STATE) ?? "1",
                    CHITDETAIL_ID = Common.NormalizeNullablePositiveLong(item.CHITDETAIL_ID),
                    CHITDETAIL_CD = NormalizeNullableText(item.CHITDETAIL_CD) ?? string.Empty,
                    SORT = item.SORT.GetValueOrDefault() > 0 ? item.SORT : index + 1,
                    ISDEL = "0"
                })
                .ToList();
        }

        private static List<InventoryOutput> NormalizeOutputs(
            IEnumerable<InventoryOutput>? outputs,
            string companyCd,
            string? headerDate,
            string chitType,
            InventoryVoucherCodeMaps? codeMaps)
        {
            if (!UsesInventoryOutput(chitType))
            {
                return new List<InventoryOutput>();
            }

            return (outputs ?? Enumerable.Empty<InventoryOutput>())
                .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                .Select((item, index) => new InventoryOutput
                {
                    OUTPUT_ID = Common.NormalizeNullablePositiveLong(item.OUTPUT_ID),
                    COGS_DEBIT = NormalizeNullableText(item.COGS_DEBIT),
                    COGS_CREDIT = NormalizeNullableText(item.COGS_CREDIT),
                    OUTPUT_CD = NormalizeNullableText(item.OUTPUT_CD) ?? string.Empty,
                    INVENTORY_ID = Common.NormalizeNullablePositiveLong(item.INVENTORY_ID),
                    INVENTORY_CD = NormalizeNullableText(item.INVENTORY_CD),
                    CHIT_TYPE = chitType,
                    COMPANY_CD = companyCd,
                    PRODUCT_ID = ResolveCodeId(item.PRODUCT_CD, codeMaps?.ProductIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.PRODUCT_ID),
                    PRODUCT_CD = NormalizeNullableText(item.PRODUCT_CD) ?? string.Empty,
                    STORE_ID = ResolveCodeId(item.STORE_CD, codeMaps?.StoreIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.STORE_ID),
                    STORE_CD = NormalizeNullableText(item.STORE_CD) ?? string.Empty,
                    UNIT_ID = ResolveCodeId(item.UNIT_CD, codeMaps?.UnitIdsByCode) ?? Common.NormalizeNullablePositiveLong(item.UNIT_ID),
                    UNIT_CD = NormalizeNullableText(item.UNIT_CD) ?? string.Empty,
                    QUANTITY = item.QUANTITY,
                    UNIT_PRICE_CC = item.UNIT_PRICE_CC,
                    FC_TYPE = NormalizeNullableText(item.FC_TYPE) ?? "VND",
                    UNIT_PRICE_FC = item.UNIT_PRICE_FC,
                    EXCHANGE_RATES = item.EXCHANGE_RATES,
                    AMOUNT_CC = chitType == "IO" ? item.AMOUNT_CC : (item.AMOUNT_CC != 0 ? item.AMOUNT_CC : item.QUANTITY * item.UNIT_PRICE_CC),
                    AMOUNT_FC = item.AMOUNT_FC,
                    SUMMARY = NormalizeNullableText(item.SUMMARY) ?? string.Empty,
                    INVENTORY_YMD = NormalizeNullableInventoryDateTimeText(item.INVENTORY_YMD, nameof(InventoryOutput.INVENTORY_YMD)) ?? headerDate,
                    STATE = NormalizeNullableText(item.STATE) ?? "1",
                    CHITDETAIL_ID = Common.NormalizeNullablePositiveLong(item.CHITDETAIL_ID),
                    CHITDETAIL_CD = NormalizeNullableText(item.CHITDETAIL_CD) ?? string.Empty,
                    SORT = item.SORT.GetValueOrDefault() > 0 ? item.SORT : index + 1,
                    ISDEL = "0"
                })
                .ToList();
        }

        private static void ValidateInventoryLines(
            string chitType,
            IReadOnlyCollection<InventoryInput> inputs,
            IReadOnlyCollection<InventoryOutput> outputs,
            string? lang)
        {
            if (chitType == "IR" && inputs.Count == 0)
            {
                throw new ArgumentException(lang == null
                    ? "INPUTS is required"
                    : ExcelImportHandlerHelper.GetInputsRequiredMessage(lang));
            }

            if (chitType == "IO" && outputs.Count == 0)
            {
                throw new ArgumentException(lang == null
                    ? "OUTPUTS is required"
                    : ExcelImportHandlerHelper.GetOutputsRequiredMessage(lang));
            }

            if (chitType == "IA" && (inputs.Count == 0 || outputs.Count == 0))
            {
                throw new ArgumentException(lang == null
                    ? "INPUTS and OUTPUTS are required"
                    : ExcelImportHandlerHelper.GetDetailsRequiredMessage(lang));
            }
        }

        private static long? ResolveCodeId(string? code, IReadOnlyDictionary<string, long>? idsByCode)
        {
            if (idsByCode == null || idsByCode.Count == 0)
            {
                return null;
            }

            return ExcelImportReferenceValidation.ResolveId(code, idsByCode);
        }
    }
}
