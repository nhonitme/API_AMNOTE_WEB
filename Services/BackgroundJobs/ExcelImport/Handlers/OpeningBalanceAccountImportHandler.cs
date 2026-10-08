using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.OpeningBalance;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class OpeningBalanceAccountImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "OpeningBalanceAccount";
        private readonly IOpeningBalanceRepository _repo;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private IReadOnlySet<string>? _eligibleAccountCodes;

        public OpeningBalanceAccountImportHandler(
            IOpeningBalanceRepository repo,
            ICompanyDatabaseResolver companyDatabaseResolver)
        {
            _repo = repo;
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
                ValidateAccountCode(rows[i], ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i), lang, validateResults);
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

            int OPEN_YMD = await _repo.GetFiscalStartYmdAsync(companyCd, databaseName);

            var records = new List<BeforeState>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                records.Add(new BeforeState
                {
                    ID = 0,
                    COMPANY_CD = companyCd,
                    OPEN_YMD = OPEN_YMD.ToString(),
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

            var savedCount = await _repo.SaveBeforeStatesAsync(
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
            if (_eligibleAccountCodes != null)
            {
                return;
            }

            var accounts = await _repo.GetEligibleAccountOptionsAsync(companyCd, lang, databaseName);
            _eligibleAccountCodes = accounts
                .Where(item => !string.IsNullOrWhiteSpace(item.CD))
                .Select(item => item.CD.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
