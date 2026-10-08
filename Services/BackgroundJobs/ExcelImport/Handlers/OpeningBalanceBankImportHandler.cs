using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.OpeningBalance;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class OpeningBalanceBankImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "OpeningBalanceBank";
        private readonly IOpeningBalanceRepository _repo;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private IReadOnlySet<string>? _eligibleAccountCodes;
        private IReadOnlyDictionary<string, long>? _accountIdsByCode;
        private IReadOnlyDictionary<string, BankInfo>? _banksByCode;

        public OpeningBalanceBankImportHandler(
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
            var databaseName = ResolveDatabaseName(request);
            await EnsureLookupMapsAsync(request.CompanyCd, lang, databaseName);

            ValidateDuplicateKeysInFile(rows, lang, validateResults);

            var openYmd = await _repo.GetFiscalStartYmdAsync(request.CompanyCd, databaseName);
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
                ValidateBankCode(rows[i], rowNo, lang, validateResults);
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

            var records = new List<BeforeStateBank>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var bankCd = Common.NormalizeNullableText(Common.GetString(row, "BANK_CD"));
                long? bankId = null;
                string bankNm = string.Empty;
                string bankAccountNo = string.Empty;
                if (!string.IsNullOrWhiteSpace(bankCd)
                    && _banksByCode != null
                    && _banksByCode.TryGetValue(bankCd, out var bank))
                {
                    bankId = bank.BANK_ID;
                    bankNm = bank.BANK_NM ?? string.Empty;
                    bankAccountNo = bank.ACCOUNT_NUM ?? string.Empty;
                }

                var accCd = Common.GetString(row, "ACC_CD");
                long? accId = null;
                var normalizedAccCd = Common.NormalizeNullableText(accCd);
                if (!string.IsNullOrWhiteSpace(normalizedAccCd)
                    && _accountIdsByCode != null
                    && _accountIdsByCode.TryGetValue(normalizedAccCd, out var resolvedAccId))
                {
                    accId = resolvedAccId;
                }

                records.Add(new BeforeStateBank
                {
                    ID = 0,
                    COMPANY_CD = companyCd,
                    OPEN_YMD = OPEN_YMD.ToString(),
                    BANK_ID = bankId,
                    BANK_CD = bankCd,
                    BANK_NM = bankNm,
                    BANK_ACCOUNT_NO = bankAccountNo,
                    ACC_ID = accId,
                    ACC_CD = accCd,
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

            var savedCount = await _repo.SaveBanksAsync(
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
                if (!TryBuildExcelDuplicateKey(rows[i], out var key, out _))
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
                            "BANK_CD",
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

            var existing = await _repo.GetBanksAsync(companyCd, openYmd);
            var existingKeys = existing
                .Select(item => BuildUniqueKey(
                    item.BANK_ID,
                    Common.NormalizeNullableText(item.BANK_ACCOUNT_NO),
                    item.ACC_ID,
                    Common.NormalizeNullableText(item.ACC_CD),
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

                if (!TryBuildDbDuplicateKey(rows[i], out var key, out var displayValue))
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
                    ExcelImportHandlerHelper.BuildRowAlreadyExistsMessage(rowNo, "BANK_CD", displayValue, lang)));
            }
        }

        private static bool TryBuildExcelDuplicateKey(
            Dictionary<string, object> row,
            out string key,
            out string displayValue)
        {
            var accCd = Common.NormalizeNullableText(Common.GetString(row, "ACC_CD"));
            var bankCd = Common.NormalizeNullableText(Common.GetString(row, "BANK_CD"));
            var fcType = Common.NormalizeNullableText(Common.GetString(row, "FC_TYPE"));

            if (string.IsNullOrWhiteSpace(accCd)
                || string.IsNullOrWhiteSpace(bankCd)
                || string.IsNullOrWhiteSpace(fcType))
            {
                key = string.Empty;
                displayValue = string.Empty;
                return false;
            }

            key = $"{accCd}|{bankCd}|{fcType}";
            displayValue = $"{accCd}/{bankCd}/{fcType}";
            return true;
        }

        private bool TryBuildDbDuplicateKey(
            Dictionary<string, object> row,
            out string key,
            out string displayValue)
        {
            var accCd = Common.NormalizeNullableText(Common.GetString(row, "ACC_CD"));
            var bankCd = Common.NormalizeNullableText(Common.GetString(row, "BANK_CD"));
            var fcType = Common.NormalizeNullableText(Common.GetString(row, "FC_TYPE"));

            if (string.IsNullOrWhiteSpace(accCd)
                || string.IsNullOrWhiteSpace(bankCd)
                || string.IsNullOrWhiteSpace(fcType)
                || _banksByCode == null
                || !_banksByCode.TryGetValue(bankCd, out var bank))
            {
                key = string.Empty;
                displayValue = string.Empty;
                return false;
            }

            long? accId = null;
            if (_accountIdsByCode != null
                && _accountIdsByCode.TryGetValue(accCd, out var resolvedAccId))
            {
                accId = resolvedAccId;
            }

            var built = BuildUniqueKey(
                bank.BANK_ID,
                Common.NormalizeNullableText(bank.ACCOUNT_NUM),
                accId,
                accCd,
                fcType);

            if (built == null)
            {
                key = string.Empty;
                displayValue = string.Empty;
                return false;
            }

            key = built;
            displayValue = $"{accCd}/{bankCd}/{fcType}";
            return true;
        }

        private static string? BuildUniqueKey(
            long? bankId,
            string? bankAccountNo,
            long? accId,
            string? accCd,
            string? fcType)
        {
            if (bankId == null || bankId <= 0
                || string.IsNullOrWhiteSpace(bankAccountNo)
                || accId == null || accId <= 0
                || string.IsNullOrWhiteSpace(accCd)
                || string.IsNullOrWhiteSpace(fcType))
            {
                return null;
            }

            // Matches uk_before_states_bank without COMPANY_CD/OPEN_YMD (scoped by query).
            return $"{bankId}|{bankAccountNo}|{accId}|{accCd}|{fcType}";
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

        private void ValidateBankCode(
            Dictionary<string, object> row,
            int rowNo,
            string lang,
            List<ExcelImportResultRowDto> validateResults)
        {
            var bankCd = Common.NormalizeNullableText(Common.GetString(row, "BANK_CD"));
            if (bankCd == null)
            {
                return;
            }

            if (_banksByCode == null || !_banksByCode.ContainsKey(bankCd))
            {
                validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, "BANK_CD", bankCd, lang));
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
                var accounts = await _repo.GetEligibleBankAccountOptionsAsync(companyCd, lang, databaseName);
                _eligibleAccountCodes = accounts
                    .Where(item => !string.IsNullOrWhiteSpace(item.CD))
                    .Select(item => item.CD.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (_accountIdsByCode == null)
            {
                _accountIdsByCode = await _lookupRepository.GetAccountIdsByCodeAsync(companyCd, databaseName);
            }

            if (_banksByCode == null)
            {
                _banksByCode = await _lookupRepository.GetBanksByCodeAsync(companyCd, databaseName);
            }
        }
    }
}
