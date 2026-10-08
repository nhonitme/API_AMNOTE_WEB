using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.OpeningBalance;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class OpeningBalanceCustomerImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "OpeningBalanceCustomer";
        private readonly IOpeningBalanceRepository _repo;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private IReadOnlySet<string>? _eligibleAccountCodes;
        private IReadOnlyDictionary<string, long>? _customerIdsByCode;

        public OpeningBalanceCustomerImportHandler(
            IOpeningBalanceRepository repo,
            IExcelImportLookupRepository lookupRepository,
            ICompanyDatabaseResolver companyDatabaseResolver)
        {
            _repo = repo;
            _lookupRepository = lookupRepository;
            _companyDatabaseResolver = companyDatabaseResolver;
        }

        public void ValidateRow(
            Dictionary<string, object> row,
            int rowNo,
            List<ExcelImportResultRowDto> validateResults)
        {
            var lang = Common.NormalizeLanguageCode(Common.GetCurrentLanguage());
            OpeningBalanceValidation.AppendExcelAmountErrors(row, rowNo, lang, validateResults);
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, lang, request.DatabaseName);

            ValidateDuplicateKeysInFile(rows, lang, validateResults);

            var openYmd = await _repo.GetFiscalStartYmdAsync(request.CompanyCd, ResolveDatabaseName(request));
            await ValidateDuplicateKeysAgainstDbAsync(
                rows,
                request.CompanyCd,
                openYmd.ToString(),
                lang,
                validateResults,
                cancellationToken);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);
                ValidateAccountCode(rows[i], rowNo, lang, validateResults);
                ValidateCustomerCode(rows[i], rowNo, lang, validateResults);
            }
        }

        public async Task SaveAsync(
            List<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            IExcelImportJobProgressWriter progressWriter,
            CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            var userId = request.UserId ?? string.Empty;
            var databaseName = ResolveDatabaseName(request);

            await EnsureLookupMapsAsync(companyCd, lang, databaseName);

            int OPEN_YMD = await _repo.GetFiscalStartYmdAsync(companyCd, databaseName);

            var records = new List<BeforeStateCustomer>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var customerCd = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_CD"));
                long? customerId = null;
                if (!string.IsNullOrWhiteSpace(customerCd)
                    && _customerIdsByCode != null
                    && _customerIdsByCode.TryGetValue(customerCd, out var resolvedId))
                {
                    customerId = resolvedId;
                }

                records.Add(new BeforeStateCustomer
                {
                    ID = 0,
                    COMPANY_CD = companyCd,
                    OPEN_YMD = OPEN_YMD.ToString(),
                    CUSTOMER_ID = customerId,
                    CUSTOMER_CD = customerCd,
                    ACC_CD = Common.GetString(row, "ACC_CD"),
                    FC_TYPE = Common.GetString(row, "FC_TYPE"),
                    DEBIT = Common.GetDecimal(row, "DEBIT"),
                    CREDIT = Common.GetDecimal(row, "CREDIT"),
                    DEBIT_FC = Common.GetDecimal(row, "DEBIT_FC"),
                    CREDIT_FC = Common.GetDecimal(row, "CREDIT_FC"),
                    EXCHANGE_RATE = Common.GetDecimal(row, "EXCHANGE_RATE"),
                    SUMMARY = "",
                    NOTE = Common.GetString(row, "NOTE"),
                    ROW_STATE = "INSERT"
                });

                var percent = 10 + (int)Math.Round((i + 1) * 20m / rows.Count);

                await progressWriter.ReportAsync(i + 1, rows.Count, percent, await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, rows.Count, lang), cancellationToken);
            }

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var savedCount = await _repo.SaveCustomersAsync(
                records,
                databaseName,
                companyCd,
                userId
            );

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(savedCount, rows.Count, lang), cancellationToken);
        }

        private static void ValidateDuplicateKeysInFile(
            IReadOnlyList<Dictionary<string, object>> rows,
            string lang,
            List<ExcelImportResultRowDto> validateResults)
        {
            var keyToRowNos = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rows.Count; i++)
            {
                if (!TryBuildDuplicateKey(rows[i], out var key, out _))
                {
                    continue;
                }

                if (!keyToRowNos.TryGetValue(key, out var rowNos))
                {
                    rowNos = new List<int>();
                    keyToRowNos[key] = rowNos;
                }

                rowNos.Add(ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i));
            }

            foreach (var pair in keyToRowNos)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                var displayValue = pair.Key.Replace('|', '/');
                foreach (var rowNo in pair.Value)
                {
                    validateResults.Add(new ExcelImportResultRowDto
                    {
                        RowNo = rowNo,
                        Status = "ERROR",
                        Message = ExcelImportHandlerHelper.BuildDuplicateFieldExceptionMessage(
                            "CUSTOMER_CD",
                            new[] { displayValue },
                            lang),
                        KeyValue = displayValue
                    });
                }
            }
        }

        private async Task ValidateDuplicateKeysAgainstDbAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            string companyCd,
            string openYmd,
            string lang,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = await _repo.GetCustomersAsync(companyCd, openYmd);
            var existingKeys = existing
                .Select(item => BuildDuplicateKey(
                    Common.NormalizeNullableText(item.ACC_CD),
                    Common.NormalizeNullableText(item.CUSTOMER_CD),
                    Common.NormalizeNullableText(item.FC_TYPE)))
                .Where(key => key != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existingKeys.Count == 0)
            {
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryBuildDuplicateKey(rows[i], out var key, out var displayValue))
                {
                    continue;
                }

                if (!existingKeys.Contains(key))
                {
                    continue;
                }

                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);
                validateResults.Add(ExcelImportHandlerHelper.BuildError(
                    rowNo,
                    ExcelImportHandlerHelper.BuildRowAlreadyExistsMessage(rowNo, "CUSTOMER_CD", displayValue, lang)));
            }
        }

        private static bool TryBuildDuplicateKey(
            Dictionary<string, object> row,
            out string key,
            out string displayValue)
        {
            var accCd = Common.NormalizeNullableText(Common.GetString(row, "ACC_CD"));
            var customerCd = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_CD"));
            var fcType = Common.NormalizeNullableText(Common.GetString(row, "FC_TYPE"));

            var built = BuildDuplicateKey(accCd, customerCd, fcType);
            if (built == null)
            {
                key = string.Empty;
                displayValue = string.Empty;
                return false;
            }

            key = built;
            displayValue = $"{accCd}/{customerCd}/{fcType}";
            return true;
        }

        private static string? BuildDuplicateKey(string? accCd, string? customerCd, string? fcType)
        {
            if (string.IsNullOrWhiteSpace(accCd)
                || string.IsNullOrWhiteSpace(customerCd)
                || string.IsNullOrWhiteSpace(fcType))
            {
                return null;
            }

            return $"{accCd}|{customerCd}|{fcType}";
        }

        private void ValidateAccountCode(
            Dictionary<string, object> row,
            int rowNo,
            string lang,
            List<ExcelImportResultRowDto> validateResults)
        {
            var accCd = Common.NormalizeNullableText(Common.GetString(row, "ACC_CD"));
            if (accCd == null)
            {
                return;
            }

            if (_eligibleAccountCodes == null || !_eligibleAccountCodes.Contains(accCd))
            {
                validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, "ACC_CD", accCd, lang));
            }
        }

        private void ValidateCustomerCode(
            Dictionary<string, object> row,
            int rowNo,
            string lang,
            List<ExcelImportResultRowDto> validateResults)
        {
            var customerCd = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_CD"));
            if (customerCd == null)
            {
                return;
            }

            if (_customerIdsByCode == null || !_customerIdsByCode.ContainsKey(customerCd))
            {
                validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, "CUSTOMER_CD", customerCd, lang));
            }
        }

        private string ResolveDatabaseName(ExcelImportJobRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.DatabaseName))
            {
                return request.DatabaseName.Trim();
            }

            return _companyDatabaseResolver.ResolveDatabaseName(request.CompanyCd);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string lang, string? databaseName)
        {
            if (_eligibleAccountCodes == null)
            {
                var accounts = await _repo.GetEligibleCustomerAccountOptionsAsync(companyCd, lang, databaseName);
                _eligibleAccountCodes = accounts
                    .Where(item => !string.IsNullOrWhiteSpace(item.CD))
                    .Select(item => item.CD.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (_customerIdsByCode == null)
            {
                _customerIdsByCode = await _lookupRepository.GetCustomerIdsByCodeAsync(companyCd, databaseName);
            }
        }
    }
}
