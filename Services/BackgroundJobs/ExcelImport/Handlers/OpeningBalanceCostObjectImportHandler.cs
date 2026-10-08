using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.OpeningBalance;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class OpeningBalanceCostObjectImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "OpeningBalanceCostObject";
        private readonly IOpeningBalanceRepository _repo;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private IReadOnlySet<string>? _eligibleAccountCodes;
        private IReadOnlyDictionary<string, long>? _accountIdsByCode;
        private IReadOnlyDictionary<string, DepartmentInfo>? _departmentsByCode;

        public OpeningBalanceCostObjectImportHandler(
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

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);
                ValidateAccountCode(rows[i], rowNo, lang, validateResults);
                ValidateDepartmentCode(rows[i], rowNo, lang, validateResults);
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

            var records = new List<BeforeStateDepartment>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var departmentCd = Common.NormalizeNullableText(Common.GetString(row, "DEPARTMENT_CD"));
                long? departmentId = null;
                string departmentNm = string.Empty;
                if (!string.IsNullOrWhiteSpace(departmentCd)
                    && _departmentsByCode != null
                    && _departmentsByCode.TryGetValue(departmentCd, out var department))
                {
                    departmentId = department.DEPARTMENT_ID;
                    departmentNm = FirstNonEmpty(
                        department.DEP_NAME_VIET,
                        department.DEP_NAME_ENG,
                        department.DEP_NAME_KOR,
                        department.DEP_NAME_CHINA);
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

                records.Add(new BeforeStateDepartment
                {
                    ID = 0,
                    COMPANY_CD = companyCd,
                    OPEN_YMD = OPEN_YMD.ToString(),
                    DEPARTMENT_ID = departmentId,
                    DEPARTMENT_CD = departmentCd,
                    DEPARTMENT_NM = departmentNm,
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

            var savedCount = await _repo.SaveDepartmentsAsync(
                records,
                databaseName,
                companyCd,
                userId
            );

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(savedCount, rows.Count, lang), cancellationToken);
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

        private void ValidateDepartmentCode(
            Dictionary<string, object> row,
            int rowNo,
            string lang,
            List<ExcelImportResultRowDto> validateResults)
        {
            var departmentCd = Common.NormalizeNullableText(Common.GetString(row, "DEPARTMENT_CD"));
            if (departmentCd == null)
            {
                return;
            }

            if (_departmentsByCode == null || !_departmentsByCode.ContainsKey(departmentCd))
            {
                validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, "DEPARTMENT_CD", departmentCd, lang));
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
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
                var accounts = await _repo.GetEligibleDepartmentAccountOptionsAsync(companyCd, lang, databaseName);
                _eligibleAccountCodes = accounts
                    .Where(item => !string.IsNullOrWhiteSpace(item.CD))
                    .Select(item => item.CD.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (_accountIdsByCode == null)
            {
                _accountIdsByCode = await _lookupRepository.GetAccountIdsByCodeAsync(companyCd, databaseName);
            }

            if (_departmentsByCode == null)
            {
                _departmentsByCode = await _lookupRepository.GetDepartmentsByCodeAsync(companyCd, databaseName);
            }
        }
    }
}
