using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Controllers
{
    public abstract partial class ChitInfoBaseController : BaseApiController
    {
        private readonly IChitInfoRepository _repository;
        private readonly IChitInfoWriteService _writeService;
        private readonly ILogger _logger;

        protected ChitInfoBaseController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger logger)
        {
            _repository = repository;
            _writeService = writeService;
            _logger = logger;
        }

        protected abstract string PermissionMenuCode { get; }
        protected abstract string InputType { get; }
        protected abstract string SupportedType { get; }
        protected abstract string HeaderCodePrefix { get; }
        protected abstract string DetailCodePrefix { get; }
        protected abstract string DocumentDisplayName { get; }
        protected virtual IReadOnlyCollection<string> SupportedTypeAliases => Array.Empty<string>();
        protected virtual bool SupportsExcelIntegration => false;
        protected virtual string? ExcelModuleCd => null;

        protected virtual IReadOnlyList<string> GetListQueryInputTypes()
            => new[] { InputType };

        [HttpGet]
        [Route("Get")]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] string? CHIT_TYPE = null, [FromQuery] long? CHIT_ID = null, [FromQuery] string? SEARCH_TEXT = null, [FromQuery] string? FROM_YMD = null, [FromQuery] string? TO_YMD = null, [FromQuery] bool INCLUDE_DETAILS = false, [FromQuery] int PAGE_NUMBER = 1, [FromQuery] int PAGE_SIZE = 20)
        {
            await EnsurePermissionAsync(PermissionMenuCode, "VIEW");

            if (CHIT_TYPE != null && NormalizeChitType(CHIT_TYPE) == null)
            {
                return ValidationError("CHIT_TYPE is invalid");
            }

            if (CHIT_ID.HasValue && CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is invalid");
            }

            var fromYmd = NormalizeNullableYmdText(FROM_YMD, nameof(FROM_YMD));
            var toYmd = NormalizeNullableYmdText(TO_YMD, nameof(TO_YMD));
            Common.ValidateYmdRange(fromYmd, toYmd);

            if (PAGE_NUMBER <= 0)
            {
                return ValidationError("PAGE_NUMBER must be greater than 0");
            }

            if (PAGE_SIZE <= 0)
            {
                return ValidationError("PAGE_SIZE must be greater than 0");
            }

            var companyCd = Common.GetCompanyCode();
            var (pageNumber, pageSize) = Common.ResolvePaging(CHIT_ID, PAGE_NUMBER, PAGE_SIZE);
            var pageResult = await QueryPagedHeadersAsync(
                companyCd,
                SupportedType,
                CHIT_ID,
                NormalizeNullableText(SEARCH_TEXT),
                fromYmd,
                toYmd,
                pageNumber,
                pageSize);
            var headers = pageResult.Items.ToList();

            if (CHIT_ID.HasValue && headers.Count == 0)
            {
                return NotFound($"{DocumentDisplayName} not found");
            }

            var shouldLoadDetails = CHIT_ID.HasValue || INCLUDE_DETAILS;
            var detailLookup = shouldLoadDetails && headers.Count > 0
                ? await LoadDetailLookupAsync(companyCd, headers, CHIT_ID)
                : new Dictionary<long, List<ChitDetailDto>>();

            var result = new List<ChitInfoDto>(headers.Count);

            foreach (var header in headers)
            {
                var dto = MapHeader(header);
                if (detailLookup.TryGetValue(header.CHIT_ID, out var details))
                {
                    dto.DETAILS = details;
                }

                result.Add(dto);
            }

            return PagedSuccess(result, pageNumber, pageSize, pageResult.TotalRecords);
        }

        [HttpPost]
        [Route("Create")]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] ChitInfoRequest? request)
        {
            await EnsurePermissionAsync(PermissionMenuCode, "ADD");
            if (!ModelState.IsValid)
            {
                return ValidationError("Model binding failed", GetModelStateErrors());
            }
            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var chitId = await _writeService.CreateAsync(companyCd, InputType, SupportedType, userId, request, SupportedTypeAliases);

            var created = (await _repository.GetChitInfosAsync(companyCd, InputType, null, chitId, null)).FirstOrDefault();
            if (created == null)
            {
                return ServerError("Create failed");
            }

            var createdDetails = (await _repository.GetChitDetailsByChitIdsAsync(companyCd, InputType, new[] { chitId })).ToList();
            var dto = MapHeader(created);
            dto.DETAILS = createdDetails.Select(MapDetail).ToList();

            return Created(dto, "Created successfully");
        }

        [HttpPut]
        [Route("Update")]
        [Authorize]
        public async Task<IActionResult> Update([FromBody] ChitInfoRequest? request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError("Model binding failed", GetModelStateErrors());
            }

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            if (!request.CHIT_ID.HasValue || request.CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var existing = (await _repository.GetChitInfosAsync(companyCd, InputType, null, request.CHIT_ID.Value, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound($"{DocumentDisplayName} not found");
            }

            var chitId = await _writeService.UpdateAsync(companyCd, InputType, SupportedType, userId, existing, request, SupportedTypeAliases);
            var updated = (await _repository.GetChitInfosAsync(companyCd, InputType, null, chitId, null)).FirstOrDefault();
            if (updated == null)
            {
                return ServerError("Update failed");
            }

            var updatedDetails = (await _repository.GetChitDetailsByChitIdsAsync(companyCd, InputType, new[] { chitId })).ToList();
            var dto = MapHeader(updated);
            dto.DETAILS = updatedDetails.Select(MapDetail).ToList();

            return Updated(dto, "Updated successfully");
        }

        [HttpDelete]
        [Route("Delete")]
        [Authorize]
        public async Task<IActionResult> Delete([FromQuery] long CHIT_ID)
        {
            await EnsurePermissionAsync(PermissionMenuCode, "DELETE");
            if (CHIT_ID <= 0)
            {
                return ValidationError("CHIT_ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetChitInfosAsync(companyCd, InputType, null, CHIT_ID, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound($"{DocumentDisplayName} not found");
            }

            var result = await _repository.DeleteChitInfoAsync(companyCd, InputType, CHIT_ID, Common.GetUserId());
            if (result >= 0)
            {
                return Deleted(CHIT_ID, "Deleted successfully");
            }

            return ServerError("Delete failed");
        }

        [HttpGet("ExportExcel")]
        [Authorize]
        public async Task<IActionResult> ExportExcel([FromQuery] long? CHIT_ID = null, [FromQuery] string? FROM_YMD = null, [FromQuery] string? TO_YMD = null)
        {
            await EnsurePermissionAsync(PermissionMenuCode, "EXPORT");
            if (!SupportsExcelIntegration || string.IsNullOrWhiteSpace(ExcelModuleCd))
            {
                return NotFound("Excel integration is not available");
            }

            if (CHIT_ID.HasValue && CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is invalid");
            }

            var fromYmd = NormalizeNullableYmdText(FROM_YMD, nameof(FROM_YMD));
            var toYmd = NormalizeNullableYmdText(TO_YMD, nameof(TO_YMD));
            Common.ValidateYmdRange(fromYmd, toYmd);

            var companyCd = Common.GetCompanyCode();
            var lang = GetCurrentLanguage();
            var templateKeys = await Common.GetExcelTemplateKeysAsync(ExcelModuleCd);
            if (templateKeys.Count == 0)
            {
                return ServerError("Excel template is not configured");
            }

            var headers = (await _repository.GetChitInfosAsync(companyCd, InputType, SupportedType, CHIT_ID, null, fromYmd, toYmd)).ToList();
            if (headers.Count == 0)
            {
                return NotFound((await getLanguage("CHIT_CD", lang)) + " " + (await getLanguage("NO_DATA_TO_EXPORT", lang)));
            }

            var exportRows = await ChitNoteExcelHelper.BuildExportRowsAsync<ChitInfo, ChitDetail>(
                headers,
                async header => await _repository.GetChitDetailsByChitIdsAsync(companyCd, InputType, new[] { header.CHIT_ID }),
                templateKeys.Keys);

            var columnMapping = await ChitNoteExcelHelper.GetColumnMappingAsync(templateKeys, lang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(templateKeys.Keys, companyCd, lang);
            var stream = await ExcelHelper.ExportToExcelAsync(exportRows, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"{SupportedType.ToLowerInvariant()}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private string? NormalizeChitType(string? value)
        {
            var normalized = NormalizeNullableText(value)?.ToUpperInvariant();

            if (normalized == null)
            {
                return null;
            }

            return normalized == SupportedType || SupportedTypeAliases.Contains(normalized)
                ? SupportedType
                : null;
        }

        private async Task<(IReadOnlyList<ChitInfo> Items, int TotalRecords)> QueryPagedHeadersAsync(
            string companyCd,
            string? chitType,
            long? chitId,
            string? searchText,
            string? fromYmd,
            string? toYmd,
            int pageNumber,
            int pageSize)
        {
            var inputTypes = GetListQueryInputTypes();

            if (inputTypes.Count == 1)
            {
                return await _repository.GetChitInfosPagedAsync(
                    companyCd,
                    inputTypes[0],
                    chitType,
                    chitId,
                    searchText,
                    fromYmd,
                    toYmd,
                    pageNumber,
                    pageSize);
            }

            return await _repository.GetChitInfosPagedByInputTypesAsync(
                companyCd,
                inputTypes,
                chitType,
                chitId,
                searchText,
                fromYmd,
                toYmd,
                pageNumber,
                pageSize);
        }

        private async Task<Dictionary<long, List<ChitDetailDto>>> LoadDetailLookupAsync(
            string companyCd,
            IReadOnlyList<ChitInfo> headers,
            long? singleChitId)
        {
            var detailLookup = new Dictionary<long, List<ChitDetailDto>>();

            foreach (var group in headers.GroupBy(
                item => string.IsNullOrWhiteSpace(item.INPUT_TYPE) ? InputType : item.INPUT_TYPE.Trim(),
                StringComparer.OrdinalIgnoreCase))
            {
                var headerIds = group.Select(item => item.CHIT_ID).Distinct().ToList();
                if (headerIds.Count == 0)
                {
                    continue;
                }

                var details = singleChitId.HasValue && headerIds.Count == 1
                    ? (await _repository.GetChitDetailsByChitIdsAsync(companyCd, group.Key, new[] { headerIds[0] })).ToList()
                    : (await _repository.GetChitDetailsByChitIdsAsync(companyCd, group.Key, headerIds)).ToList();

                foreach (var detailGroup in details.GroupBy(item => item.CHIT_ID))
                {
                    detailLookup[detailGroup.Key] = detailGroup.Select(MapDetail).ToList();
                }
            }

            return detailLookup;
        }

        private ChitInfoDto MapHeader(ChitInfo item)
        {
            return new ChitInfoDto
            {
                CHIT_ID = item.CHIT_ID,
                COMPANY_CD = item.COMPANY_CD,
                CHIT_CD = item.CHIT_CD,
                CHIT_NO = item.CHIT_NO,
                CHIT_YMD = item.CHIT_YMD,
                CHIT_TYPE = NormalizeChitType(item.CHIT_TYPE) ?? SupportedType,
                INPUT_TYPE = string.IsNullOrWhiteSpace(item.INPUT_TYPE) ? InputType : item.INPUT_TYPE.Trim(),
                LOCK_STEP_CODE = item.LOCK_STEP_CODE,
                AMOUNT = item.AMOUNT,
                PAYER_INFO = item.PAYER_INFO,
                ISDEL = item.ISDEL,
                IS_LOCK = item.IS_LOCK,
                ISEXCEL = item.ISEXCEL,
                EMAIL_EPAY = item.EMAIL_EPAY,
                IS_CONFIRMED = item.IS_CONFIRMED,
                NOTE = item.NOTE,
                DAY_OF_PAYMENT = item.DAY_OF_PAYMENT,
                TIME_FOR_PAYMENT = item.TIME_FOR_PAYMENT,
                IS_PAYMENT = item.IS_PAYMENT,
                CHIT_CD_COGS = item.CHIT_CD_COGS,
                DESCRIPTION_VIET = item.DESCRIPTION_VIET,
                DESCRIPTION_ENG = item.DESCRIPTION_ENG,
                DESCRIPTION_KOR = item.DESCRIPTION_KOR,
                DETAIL_COUNT = item.DETAIL_COUNT
            };
        }

        private static ChitDetailDto MapDetail(ChitDetail item)
        {
            return new ChitDetailDto
            {
                CHITDETAIL_ID = item.CHITDETAIL_ID,
                COMPANY_CD = item.COMPANY_CD,
                CHIT_ID = item.CHIT_ID,
                CHITDETAIL_CD = item.CHITDETAIL_CD,
                CHIT_YMD = item.CHIT_YMD,
                CHIT_VMD = item.CHIT_VMD,
                DEBIT = item.DEBIT,
                DEBIT_NM_VIET = item.DEBIT_NM_VIET,
                DEBIT_NM_ENG = item.DEBIT_NM_ENG,
                DEBIT_NM_KOR = item.DEBIT_NM_KOR,
                DEBIT_NM_CHINA = item.DEBIT_NM_CHINA,
                CREDIT = item.CREDIT,
                CREDIT_NM_VIET = item.CREDIT_NM_VIET,
                CREDIT_NM_ENG = item.CREDIT_NM_ENG,
                CREDIT_NM_KOR = item.CREDIT_NM_KOR,
                CREDIT_NM_CHINA = item.CREDIT_NM_CHINA,
                AMOUNT = item.AMOUNT,
                FC_AMOUNT = item.FC_AMOUNT,
                FC_TYPE = item.FC_TYPE,
                FC_RATE = item.FC_RATE,
                FC_DATETIME = item.FC_DATETIME,
                SORT = item.SORT,
                ISDEL = item.ISDEL,
                CHITDETAIL_VAT_CD = item.CHITDETAIL_VAT_CD,
                MG_CD = item.MG_CD,
                MG_CD_2 = item.MG_CD_2,
                MR_CD = item.MR_CD,
                MR_CD2 = item.MR_CD2,
                BANK_ID = item.BANK_ID,
                BANK_CD = item.BANK_CD,
                BANK_OWN_CD = item.BANK_OWN_CD,
                CUSTOMER_ID = item.CUSTOMER_ID,
                CUSTOMER_CD = item.CUSTOMER_CD,
                CUSTOMER_NM_VIET = item.CUSTOMER_NM_VIET,
                CUSTOMER_NM_ENG = item.CUSTOMER_NM_ENG,
                CUSTOMER_NM_KOR = item.CUSTOMER_NM_KOR,
                CUSTOMER_NM_CHINA = item.CUSTOMER_NM_CHINA,
                CUSTOMER_OWN_CD = item.CUSTOMER_OWN_CD,
                DEPARTMENT_ID = item.DEPARTMENT_ID,
                DEPARTMENT_CD = item.DEPARTMENT_CD,
                DEPARTMENT_CD_2 = item.DEPARTMENT_CD_2,
                HASINVENTORY = item.HASINVENTORY,
                INVENTORY_YMD = item.INVENTORY_YMD,
                ISPAY = item.ISPAY,
                ISCOLLECT = item.ISCOLLECT,
                VAT_SERIAL_NO = item.VAT_SERIAL_NO,
                VAT_CHIT_NO = item.VAT_CHIT_NO,
                VAT_CHIT_NO_2 = item.VAT_CHIT_NO_2,
                VAT_AMOUNT = item.VAT_AMOUNT,
                VAT_TAXABLE_AMOUNT = item.VAT_TAXABLE_AMOUNT,
                FO_VAT_AMOUNT = item.FO_VAT_AMOUNT,
                VAT_ISFREE = item.VAT_ISFREE,
                VAT_INVOICE_CD = item.VAT_INVOICE_CD,
                VAT_INVOICE_NM = item.VAT_INVOICE_NM,
                VAT_INFO_TYPE = item.VAT_INFO_TYPE,
                VAT_COMPANY_ISSUE = item.VAT_COMPANY_ISSUE,
                VAT_COMPANY_ISSUE_ADDRESS = item.VAT_COMPANY_ISSUE_ADDRESS,
                VAT_COMPANY_ISSUE_CD = item.VAT_COMPANY_ISSUE_CD,
                VAT_COMPANY_TAXCD = item.VAT_COMPANY_TAXCD,
                VAT_PRODUCT_NM = item.VAT_PRODUCT_NM,
                VAT_ETC = item.VAT_ETC,
                VAT_YMD = item.VAT_YMD,
                VAT_YMD_2 = item.VAT_YMD_2,
                VAT_INQUIRY_IN = item.VAT_INQUIRY_IN,
                VAT_INQUIRY_CODE = item.VAT_INQUIRY_CODE,
                IS_NEXTVAT = item.IS_NEXTVAT,
                UNDEFINE = item.UNDEFINE,
                DETAIL_DESCRIPTION_VIET = item.DETAIL_DESCRIPTION_VIET,
                DETAIL_DESCRIPTION_ENG = item.DETAIL_DESCRIPTION_ENG,
                DETAIL_DESCRIPTION_KOR = item.DETAIL_DESCRIPTION_KOR,
            };
        }
    }
}
