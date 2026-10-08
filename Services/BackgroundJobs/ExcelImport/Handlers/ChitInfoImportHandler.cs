using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Threading;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class ChitInfoImportHandler : IExcelImportModuleHandler, IExcelImportModuleAliasProvider
    {
        private static readonly Dictionary<string, ChitImportModule> Modules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentVoucherAp"] = new("AP", "PM"),
            ["DebitNoteAp"] = new("AP", "DN"),
            ["PurchaseVoucherAp"] = new("AP", "PO"),
            ["PurchaseServiceVoucherAp"] = new("AP", "PS"),
            ["PurchaseDiscountVoucherAp"] = new("AP", "PD"),
            ["PurchaseReturnVoucherAp"] = new("AP", "PR"),
            ["OffsetVoucherAp"] = new("AP", "CO"),
            ["OtherVoucherAp"] = new("AP", "OT"),
            ["ReceiptVoucherAr"] = new("AR", "RC"),
            ["CreditNoteAr"] = new("AR", "CN"),
            ["SalesVoucherAr"] = new("AR", "SO"),
            ["SalesDiscountVoucherAr"] = new("AR", "SD"),
            ["SalesReturnVoucherAr"] = new("AR", "SR"),
            ["OffsetVoucherAr"] = new("AR", "CO"),
            ["OtherVoucherAr"] = new("AR", "OT")
        };

        private readonly IChitInfoWriteService _writeService;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _accountIdsByCode;
        private IReadOnlyDictionary<string, long>? _bankIdsByCode;
        private IReadOnlyDictionary<string, long>? _customerIdsByCode;
        private IReadOnlyDictionary<string, long>? _departmentIdsByCode;
        private IReadOnlyDictionary<string, long>? _managementIdsByCode;

        public ChitInfoImportHandler(IChitInfoWriteService writeService, IExcelImportLookupRepository lookupRepository)
        {
            _writeService = writeService;
            _lookupRepository = lookupRepository;
        }

        public string ModuleCd => "ChitInfo";
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

                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "DEBIT_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_DEBIT_CD", "DETAIL_DEBIT", "DEBIT_CD", "DEBIT"), _accountIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "CREDIT_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_CREDIT_CD", "DETAIL_CREDIT", "CREDIT_CD", "CREDIT"), _accountIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "BANK_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_BANK_CD", "BANK_CD"), _bankIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "CUSTOMER_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_CUSTOMER_CD", "CUSTOMER_CD"), _customerIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "DEPARTMENT_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_DEPARTMENT_CD", "DEPARTMENT_CD"), _departmentIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "DEPARTMENT_CD_2", ExcelImportReferenceValidation.GetString(row, "DETAIL_DEPARTMENT_CD_2", "DEPARTMENT_CD_2"), _departmentIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "MG_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_MG_CD", "MG_CD"), _managementIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "MG_CD_2", ExcelImportReferenceValidation.GetString(row, "DETAIL_MG_CD_2", "MG_CD_2"), _managementIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "MR_CD", ExcelImportReferenceValidation.GetString(row, "DETAIL_MR_CD", "MR_CD"), _managementIdsByCode!, lang);
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, rowNo, "MR_CD2", ExcelImportReferenceValidation.GetString(row, "DETAIL_MR_CD2", "MR_CD2"), _managementIdsByCode!, lang);
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var lang = request.Lang ?? Common.GetCurrentLanguage();
            var module = ResolveModule(request.ModuleCd, lang);
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);
            var templateKeys = (await Common.GetExcelTemplateKeysAsync(request.ModuleCd, companyCd)).Keys.ToList();
            var recordsResult = await ChitNoteExcelHelper.BuildImportRecordsAsync<ChitInfoRequest, ChitDetailRequest>(rows, templateKeys, module.ChitType, lang);
            var errors = new List<string>(recordsResult.Errors);

            errors = ChitNoteExcelHelper.NormalizeImportErrors(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }

            var orderedRecords = recordsResult.Records.OrderBy(item => item.Row).ToList();
            var codeMaps = new ChitInfoCodeMaps
            {
                BankIdsByCode = _bankIdsByCode,
                CustomerIdsByCode = _customerIdsByCode,
                DepartmentIdsByCode = _departmentIdsByCode
            };

            // Cache progress text once — avoid DB round-trip per row (can hang/starve under load).
            var transferTemplate = await ExcelImportHandlerHelper.GetLocalizedMessageAsync("EXCEL_IMPORT_TRANSFER", lang);
            var transferSuffix = await ExcelImportHandlerHelper.GetLocalizedMessageAsync("EXCEL_IMPORT_TRANSFER_SUFFIX", lang);

            for (var i = 0; i < orderedRecords.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = orderedRecords[i];
                item.Record.ISEXCEL = "1";

                // Soft watchdog independent of job CT — production previously hung with no timeout log.
                const int rowWatchdogSec = 45;
                var saveTask = _writeService.CreateAsync(
                    companyCd,
                    module.InputType,
                    module.ChitType,
                    userId,
                    item.Record,
                    null,
                    codeMaps,
                    request.DatabaseName,
                    lang);
                var delayTask = Task.Delay(TimeSpan.FromSeconds(rowWatchdogSec));
                var completed = await Task.WhenAny(saveTask, delayTask);
                if (completed != saveTask)
                {
                    throw new TimeoutException(
                        $"Save chit timeout after {rowWatchdogSec}s at row {item.Row} ({i + 1}/{orderedRecords.Count}), CHIT_NO={item.Record.CHIT_NO}");
                }

                await saveTask;

                var progressMessage = $"{transferTemplate} {i + 1}/{orderedRecords.Count} {transferSuffix}";
                var percent = 40 + (int)Math.Round((i + 1) * 60m / Math.Max(orderedRecords.Count, 1));
                await progressWriter.ReportAsync(
                    i + 1,
                    orderedRecords.Count,
                    percent,
                    progressMessage,
                    CancellationToken.None);
            }
        }

        private static ChitImportModule ResolveModule(string moduleCd, string lang)
        {
            if (Modules.TryGetValue(moduleCd, out var module))
            {
                return module;
            }

            throw new InvalidOperationException(ExcelImportHandlerHelper.GetUnsupportedModuleMessage(moduleCd, lang));
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _accountIdsByCode ??= await _lookupRepository.GetAccountIdsByCodeAsync(companyCd, databaseName);
            _bankIdsByCode ??= await _lookupRepository.GetBankIdsByCodeAsync(companyCd, databaseName);
            _customerIdsByCode ??= await _lookupRepository.GetCustomerIdsByCodeAsync(companyCd, databaseName);
            _departmentIdsByCode ??= await _lookupRepository.GetDepartmentIdsByCodeAsync(companyCd, databaseName);
            _managementIdsByCode ??= await _lookupRepository.GetManagementIdsByCodeAsync(companyCd, databaseName);
        }

        private sealed record ChitImportModule(string InputType, string ChitType);
    }
}
