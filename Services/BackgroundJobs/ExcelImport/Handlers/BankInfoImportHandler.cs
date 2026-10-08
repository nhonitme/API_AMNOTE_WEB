using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class BankInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "BankInfo";
        private readonly IBankInfoService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _accountIdsByCode;

        public BankInfoImportHandler(IBankInfoService service, IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }
        public async Task ValidateRowsAsync(IReadOnlyList<Dictionary<string, object>> rows, ExcelImportJobRequest request, List<ExcelImportResultRowDto> validateResults, CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExcelImportReferenceValidation.AddMissingCodeError(validateResults, ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i), "ACC_CD", Common.GetString(rows[i], "ACC_CD"), _accountIdsByCode!, lang);
            }
        }

        public async Task SaveAsync(List<Dictionary<string, object>> rows, ExcelImportJobRequest request, IExcelImportJobProgressWriter progressWriter, CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);

            var records = rows.Select(row => new BankInfoRequest
            {
                BANK_ID = 0,
                COMPANY_CD = companyCd,
                BANK_CD = Common.NormalizeRequiredText(Common.GetString(row, "BANK_CD")),
                BANK_NM = Common.NormalizeRequiredText(Common.GetString(row, "BANK_NM")),
                ACC_CD = Common.NormalizeNullableText(Common.GetString(row, "ACC_CD")),
                PASSBOOK_NM = Common.NormalizeNullableText(Common.GetString(row, "PASSBOOK_NM")),
                ACCOUNT_NUM = Common.NormalizeNullableText(Common.GetString(row, "ACCOUNT_NUM")),
                CITAD_CODE = Common.NormalizeNullableText(Common.GetString(row, "CITAD_CODE")),
                REMARK = Common.NormalizeNullableText(Common.GetString(row, "REMARK")),
                ISDEL = Common.NormalizeFlagString(Common.GetString(row, "ISDEL"), "0"),
                CREATE_BY = userId,
                UPDATE_BY = userId
            }).ToList();

            await progressWriter.ReportPercentAsync(35, await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang), cancellationToken);

            var inserted = await _service.BulkInsertAsync(companyCd, userId, records, request.DatabaseName, lang);
            if (inserted <= 0)
            {
                throw new InvalidOperationException(ExcelImportHandlerHelper.GetImportFailedMessage(lang));
            }

            await progressWriter.ReportPercentAsync(100, await ExcelImportHandlerHelper.GetCompleteTextAsync(inserted, rows.Count, lang), cancellationToken);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _accountIdsByCode ??= await _lookupRepository.GetAccountIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
