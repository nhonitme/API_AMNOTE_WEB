using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public sealed class CustomerInfoCustomerExtImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "CustomerInfoCustomerExt";
        private readonly ICustomerInfoService _service;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _bankIdsByCode;

        public CustomerInfoCustomerExtImportHandler(ICustomerInfoService service, IExcelImportLookupRepository lookupRepository)
        {
            _service = service;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // BANK_CD optional — same SoftOptional pattern as ProductInfo kind/store.
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(rows[i], i);
                ExcelImportReferenceValidation.SoftOptionalCode(
                    validateResults, rows, i, rowNo, "BANK_CD", _bankIdsByCode!, lang);
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
            var now = DateTime.Now;
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);

            var records = rows.Select(row => new CustomerInfoCustomerExtRequest
            {
                CUSTOMER_ID = 0,
                CUSTOMER_EXT_ID = 0,
                COMPANY_CD = companyCd,
                CUSTOMER_CD = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_CD")),
                CATEGORY_CD = Common.NormalizeNullableText(Common.GetString(row, "CATEGORY_CD")),
                CUSTOMER_TYPE = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_TYPE")),
                CUSTOMER_NM_VIET = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_NM_VIET")),
                CUSTOMER_NM_ENG = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_NM_ENG")),
                CUSTOMER_NM_KOR = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_NM_KOR")),
                CUSTOMER_NM_CHINA = Common.NormalizeNullableText(Common.GetString(row, "CUSTOMER_NM_CHINA")),
                ADDRESS = Common.NormalizeNullableText(Common.GetString(row, "ADDRESS")),
                TEL = Common.NormalizeNullableText(Common.GetString(row, "TEL")),
                FAX = Common.NormalizeNullableText(Common.GetString(row, "FAX")),
                TAX_CD = Common.NormalizeNullableText(Common.GetString(row, "TAX_CD")),
                BANK_ID = ExcelImportReferenceValidation.ResolveId(Common.GetString(row, "BANK_CD"), _bankIdsByCode!),
                BANK_CD = Common.NormalizeNullableText(Common.GetString(row, "BANK_CD")),
                EMAIL = Common.NormalizeNullableText(Common.GetString(row, "EMAIL")),
                NOTE = Common.NormalizeNullableText(Common.GetString(row, "NOTE")),
                IDNUMBER = Common.NormalizeNullableText(Common.GetString(row, "IDNUMBER")),
                BUYER_NM = Common.NormalizeNullableText(Common.GetString(row, "BUYER_NM")),
                ISDEL = Common.NormalizeFlagString(Common.GetString(row, "ISDEL"), "0"),
                CREATE_AT = now,
                UPDATE_AT = now,
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
            _bankIdsByCode ??= await _lookupRepository.GetBankIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
