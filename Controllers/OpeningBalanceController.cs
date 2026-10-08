using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.OpeningBalance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class OpeningBalanceController : BaseApiController
    {
        private readonly IOpeningBalanceRepository _repo;
        private readonly ILogger<OpeningBalanceController> _logger;

        public OpeningBalanceController(IOpeningBalanceRepository repo, ILogger<OpeningBalanceController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] string? openYmd = null, [FromQuery] string? p_KEYWORD = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetDepartmentsAsync(companyCd, openYmd, p_KEYWORD);
            return Success(data);
        }

        [HttpPost("departments")]
        public async Task<IActionResult> SaveDepartments([FromBody] List<BeforeStateDepartment>? records)
        {
            if (records == null || records.Count == 0)
                return ValidationError("Records are required");

            try
            {
                foreach (var rec in records)
                {
                    rec.COMPANY_CD = Common.GetCompanyCode();
                    OpeningBalanceValidation.EnsureAmountsValid(rec);
                }

                var result = await _repo.SaveDepartmentsAsync(records);
                if (result <= 0)
                    return ServerError("Save failed");

                return Created(new { saved = result }, "Saved successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers([FromQuery] string? openYmd = null, [FromQuery] string? p_KEYWORD = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetCustomersAsync(companyCd, openYmd, p_KEYWORD);
            return Success(data);
        }

        [HttpPost("customers")]
        public async Task<IActionResult> SaveCustomers([FromBody] List<BeforeStateCustomer>? records)
        {
            if (records == null || records.Count == 0)
                return ValidationError("Records are required");

            try
            {
                foreach (var rec in records)
                {
                    rec.COMPANY_CD = Common.GetCompanyCode();
                    OpeningBalanceValidation.EnsureAmountsValid(rec);
                }

                var result = await _repo.SaveCustomersAsync(records);
                if (result <= 0)
                    return ServerError("Save failed");

                return Created(new { saved = result }, "Saved successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpGet("banks")]
        public async Task<IActionResult> GetBanks([FromQuery] string? openYmd = null, [FromQuery] string? p_KEYWORD = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetBanksAsync(companyCd, openYmd, p_KEYWORD);
            return Success(data);
        }

        [HttpPost("banks")]
        public async Task<IActionResult> SaveBanks([FromBody] List<BeforeStateBank>? records)
        {
            if (records == null || records.Count == 0)
                return ValidationError("Records are required");

            try
            {
                foreach (var rec in records)
                {
                    rec.COMPANY_CD = Common.GetCompanyCode();
                    OpeningBalanceValidation.EnsureAmountsValid(rec);
                }

                var result = await _repo.SaveBanksAsync(records);
                if (result <= 0)
                    return ServerError("Save failed");

                return Created(new { saved = result }, "Saved successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBeforeStates([FromQuery] string? openYmd = null, [FromQuery] string? p_KEYWORD = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetBeforeStatesAsync(companyCd, openYmd, p_KEYWORD);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> SaveBeforeStates([FromBody] List<BeforeState>? records)
        {
            if (records == null || records.Count == 0)
                return ValidationError("Records are required");

            try
            {
                foreach (var rec in records)
                {
                    rec.COMPANY_CD = Common.GetCompanyCode();
                    OpeningBalanceValidation.EnsureAmountsValid(rec);
                }

                var result = await _repo.SaveBeforeStatesAsync(records);
                if (result <= 0)
                    return ServerError("Save failed");

                return Created(new { saved = result }, "Saved successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpGet("account-options")]
        public async Task<IActionResult> GetEligibleAccountOptions([FromQuery] string? lang = null)
        {
            var companyCd = Common.GetCompanyCode();
            var accounts = await _repo.GetEligibleAccountOptionsAsync(companyCd, lang);
            return Success(MapAccountOptions(accounts));
        }

        [HttpGet("customer-account-options")]
        public async Task<IActionResult> GetEligibleCustomerAccountOptions([FromQuery] string? lang = null)
        {
            var companyCd = Common.GetCompanyCode();
            var accounts = await _repo.GetEligibleCustomerAccountOptionsAsync(companyCd, lang);
            return Success(MapAccountOptions(accounts));
        }

        [HttpGet("bank-account-options")]
        public async Task<IActionResult> GetEligibleBankAccountOptions([FromQuery] string? lang = null)
        {
            var companyCd = Common.GetCompanyCode();
            var accounts = await _repo.GetEligibleBankAccountOptionsAsync(companyCd, lang);
            return Success(MapAccountOptions(accounts));
        }

        [HttpGet("department-account-options")]
        public async Task<IActionResult> GetEligibleDepartmentAccountOptions([FromQuery] string? lang = null)
        {
            var companyCd = Common.GetCompanyCode();
            var accounts = await _repo.GetEligibleDepartmentAccountOptionsAsync(companyCd, lang);
            return Success(MapAccountOptions(accounts));
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetOpeningBalanceSummary([FromQuery] string? openYmd = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repo.GetOpeningBalanceSummaryAsync(companyCd, openYmd);
            return Success(data);
        }

        /// <summary>
        /// Lấy năm đầu kỳ kế toán của công ty.
        /// Store có thể đọc từ sys_config hoặc bảng cấu hình công ty.
        /// </summary>
        [HttpGet("FiscalStartYear")]
        public async Task<IActionResult> GetFiscalStartYear([FromQuery] string? companyCd = null)
        {
            companyCd = companyCd ?? Common.GetCompanyCode();
            var fiscalStartYmd = await _repo.GetFiscalStartYmdAsync(companyCd);

            if (fiscalStartYmd <= 0)
                return ServerError("Không lấy được năm đầu kỳ");

            return Success(fiscalStartYmd);
        }

        /// <summary>
        /// Export danh sách số dư tài khoản đầu kỳ ra Excel.
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportBeforeStates(
            [FromQuery] long? ID = null,
            [FromQuery] string? openYmd = null,
            [FromQuery] string? lang = null)
        {
            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();

            var data = (await _repo.GetBeforeStatesAsync(companyCd, openYmd)).ToList();
            if (ID.HasValue && ID.Value > 0)
            {
                data = data.Where(x => x.ID == ID.Value).ToList();
            }

            if (!data.Any())
            {
                return NotFound(
                    (await Common.getLanguage("ACC_CD", currentLang))
                    + " "
                    + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string moduleCd = "OpeningBalanceAccount";
            const string screenCd = "/module/opening-balance/account";
            const string gridId = "opening-balance-account-grid";

            var exportColumns = await Common.GetExcelExportColumnInfosAsync(moduleCd, screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"opening_balance_account_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Export danh sách số dư khách hàng đầu kỳ ra Excel.
        /// </summary>
        [HttpGet("customers/export")]
        public async Task<IActionResult> ExportCustomers(
            [FromQuery] long? ID = null,
            [FromQuery] string? openYmd = null,
            [FromQuery] string? lang = null)
        {
            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();

            var data = (await _repo.GetCustomersAsync(companyCd, openYmd)).ToList();
            if (ID.HasValue && ID.Value > 0)
            {
                data = data.Where(x => x.ID == ID.Value).ToList();
            }

            if (!data.Any())
            {
                return NotFound(
                    (await Common.getLanguage("CUSTOMER_CD", currentLang))
                    + " "
                    + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string moduleCd = "OpeningBalanceCustomer";
            const string screenCd = "/module/opening-balance/customer";
            const string gridId = "opening-balance-customer-grid";

            var exportColumns = await Common.GetExcelExportColumnInfosAsync(moduleCd, screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"opening_balance_customer_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Export danh sách số dư ngân hàng đầu kỳ ra Excel.
        /// </summary>
        [HttpGet("banks/export")]
        public async Task<IActionResult> ExportBanks(
            [FromQuery] long? ID = null,
            [FromQuery] string? openYmd = null,
            [FromQuery] string? lang = null)
        {
            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();

            var data = (await _repo.GetBanksAsync(companyCd, openYmd)).ToList();
            if (ID.HasValue && ID.Value > 0)
            {
                data = data.Where(x => x.ID == ID.Value).ToList();
            }

            if (!data.Any())
            {
                return NotFound(
                    (await Common.getLanguage("BANK_CD", currentLang))
                    + " "
                    + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string moduleCd = "OpeningBalanceBank";
            const string screenCd = "/module/opening-balance/bank";
            const string gridId = "opening-balance-bank-grid";

            var exportColumns = await Common.GetExcelExportColumnInfosAsync(moduleCd, screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"opening_balance_bank_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Export danh sách số dư đối tượng THCP đầu kỳ ra Excel.
        /// </summary>
        [HttpGet("departments/export")]
        public async Task<IActionResult> ExportDepartments(
            [FromQuery] long? ID = null,
            [FromQuery] string? openYmd = null,
            [FromQuery] string? lang = null)
        {
            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();

            var data = (await _repo.GetDepartmentsAsync(companyCd, openYmd)).ToList();
            if (ID.HasValue && ID.Value > 0)
            {
                data = data.Where(x => x.ID == ID.Value).ToList();
            }

            if (!data.Any())
            {
                return NotFound(
                    (await Common.getLanguage("DEPARTMENT_CD", currentLang))
                    + " "
                    + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string moduleCd = "OpeningBalanceCostObject";
            const string screenCd = "/module/opening-balance/cost-object";
            const string gridId = "opening-balance-cost-object-grid";

            var exportColumns = await Common.GetExcelExportColumnInfosAsync(moduleCd, screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"opening_balance_cost_object_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static IEnumerable<object> MapAccountOptions(IEnumerable<EtcInfo> accounts)
            => accounts.Select(account => new
            {
                ID = account.ID,
                CD = account.CD,
                NM_VIET = account.NM_VIET,
                NM_ENG = account.NM_ENG,
                NM_KOR = account.NM_KOR,
                NM_CHINA = account.NM_CHINA
            });
    }
}
