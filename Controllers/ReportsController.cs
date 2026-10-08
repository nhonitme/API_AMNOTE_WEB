using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : BaseApiController
    {
        private readonly IConfiguredReportService _configuredReportService;
        private readonly IReportOptionRepository _reportOptionRepository;
        private readonly ICashFlowFormulaOptionRepository _cashFlowFormulaOptionRepository;

        public ReportsController(
            IConfiguredReportService configuredReportService,
            IReportOptionRepository reportOptionRepository,
            ICashFlowFormulaOptionRepository cashFlowFormulaOptionRepository)
        {
            _configuredReportService = configuredReportService ?? throw new ArgumentNullException(nameof(configuredReportService));
            _reportOptionRepository = reportOptionRepository ?? throw new ArgumentNullException(nameof(reportOptionRepository));
            _cashFlowFormulaOptionRepository = cashFlowFormulaOptionRepository ?? throw new ArgumentNullException(nameof(cashFlowFormulaOptionRepository));
        }

        [HttpGet("pdf")]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] string? reportCode = null,
            [FromQuery] string? menuCode = null,
            [FromQuery] string? reportGroupCode = null)
        {
            var effectiveCompanyCd = Common.GetCompanyCode();
            var selection = await ResolveReportSelectionAsync(
                effectiveCompanyCd,
                reportCode,
                menuCode,
                reportGroupCode,
                HttpContext.RequestAborted);
            var query = BuildReportQuery();
            var report = await _configuredReportService.BuildReportAsync(
                effectiveCompanyCd,
                selection.ReportCode,
                selection.MenuCode,
                query,
                HttpContext.RequestAborted);

            using var stream = new MemoryStream();
            report.ExportToPdf(stream);
            stream.Position = 0;

            var fileName = string.IsNullOrWhiteSpace(report.DisplayName)
                ? "Report.pdf"
                : $"{report.DisplayName.Trim()}.pdf";

            return File(stream.ToArray(), "application/pdf", fileName);
        }

        [HttpGet("preview")]
        public async Task<IActionResult> GetPreview(
            [FromQuery] string? reportCode = null,
            [FromQuery] string? menuCode = null,
            [FromQuery] string? reportGroupCode = null)
        {
            var effectiveCompanyCd = Common.GetCompanyCode();
            var selection = await ResolveReportSelectionAsync(
                effectiveCompanyCd,
                reportCode,
                menuCode,
                reportGroupCode,
                HttpContext.RequestAborted);
            var query = BuildReportQuery();
            var data = await _configuredReportService.BuildPreviewAsync(
                effectiveCompanyCd,
                selection.ReportCode,
                selection.MenuCode,
                query,
                HttpContext.RequestAborted);
            return Success(data);
        }

        [HttpGet("options")]
        public async Task<IActionResult> GetOptions([FromQuery] string reportGroupCode)
        {
            var effectiveCompanyCd = Common.GetCompanyCode();

            var options = await _reportOptionRepository.GetReportOptionsAsync(
                effectiveCompanyCd,
                reportGroupCode,
                HttpContext.RequestAborted);

            return Success(options.Select(MapOption).ToList());
        }

        [HttpGet("formula-options")]
        public async Task<IActionResult> GetCashFlowFormulaOptions(
            [FromQuery] string? reportCode = null,
            [FromQuery] string? reportVersion = null)
        {
            await EnsureFormulaOptionsPermissionAsync(reportCode);

            try
            {
                var companyCd = Common.GetCompanyCode();
                var options = await _cashFlowFormulaOptionRepository.GetAsync(
                    companyCd,
                    reportCode,
                    reportVersion,
                    HttpContext.RequestAborted);

                return Success(options);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpPut("formula-options")]
        public async Task<IActionResult> SaveCashFlowFormulaOptions(
            [FromBody] SaveCashFlowFormulaOptionsRequest? request)
        {
            await EnsureFormulaOptionsPermissionAsync(request?.REPORT_CODE);

            if (request == null)
            {
                return ValidationError("Request body must be provided.");
            }

            try
            {
                var companyCd = Common.GetCompanyCode();
                var options = await _cashFlowFormulaOptionRepository.SaveAsync(
                    companyCd,
                    request,
                    Common.GetUserId(),
                    HttpContext.RequestAborted);

                return Updated(options, "Updated successfully");
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpPost("formula-options/preview")]
        public async Task<IActionResult> PreviewCashFlowFormulaOptions(
            [FromBody] PreviewCashFlowFormulaOptionsRequest? request)
        {
            await EnsureFormulaOptionsPermissionAsync(request?.REPORT_CODE);

            if (request == null)
            {
                return ValidationError("Request body must be provided.");
            }

            try
            {
                var companyCd = Common.GetCompanyCode();
                var preview = await _cashFlowFormulaOptionRepository.PreviewDraftAsync(
                    companyCd,
                    request,
                    Common.GetUserId(),
                    _configuredReportService,
                    HttpContext.RequestAborted);

                return Success(preview);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("formula-options/reset")]
        public async Task<IActionResult> ResetCashFlowFormulaOptions(
            [FromQuery] string? reportCode = null,
            [FromQuery] string? reportVersion = null)
        {
            await EnsureFormulaOptionsPermissionAsync(reportCode);

            try
            {
                var companyCd = Common.GetCompanyCode();
                var options = await _cashFlowFormulaOptionRepository.ResetToDefaultAsync(
                    companyCd,
                    reportCode,
                    reportVersion,
                    Common.GetUserId(),
                    HttpContext.RequestAborted);

                return Success(options, "Reset successfully");
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        private async Task EnsureFormulaOptionsPermissionAsync(string? reportCode)
        {
            var permissionMenuCode = CashFlowFormulaOptionRepository.ResolvePermissionMenuCode(reportCode);
            await EnsurePermissionAsync(permissionMenuCode, "EDIT");
        }

        private static ReportOptionDto MapOption(ReportOptionInfo source)
        {
            return new ReportOptionDto
            {
                OPTION_ID = source.OPTION_ID,
                COMPANY_CD = source.COMPANY_CD,
                REPORT_GROUP_CODE = source.REPORT_GROUP_CODE,
                OPTION_CODE = source.OPTION_CODE,
                OPTION_NAME = source.OPTION_NAME,
                LABEL_TEXT = Common.NormalizeNullableText(source.LABEL_TEXT),
                CAPTION = string.IsNullOrWhiteSpace(source.CAPTION) ? source.OPTION_NAME : source.CAPTION,
                REPORT_CODE = source.REPORT_CODE,
                IS_DEFAULT = source.IS_DEFAULT,
                SORT_ORDER = source.SORT_ORDER
            };
        }

        private Dictionary<string, string> BuildReportQuery()
        {
            return Request.Query
                .Where(item => Common.NormalizeToken(item.Key) is not "companycd" and not "pcompanycd")
                .ToDictionary(item => item.Key, item => item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }

        private async Task<ReportSelection> ResolveReportSelectionAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            string? reportGroupCode,
            CancellationToken cancellationToken)
        {
            var effectiveReportCode = Common.NormalizeNullableText(reportCode);
            var effectiveMenuCode = Common.NormalizeNullableText(menuCode);
            var effectiveReportGroupCode = Common.NormalizeNullableText(reportGroupCode);

            if (!string.IsNullOrWhiteSpace(effectiveReportCode) || !string.IsNullOrWhiteSpace(effectiveMenuCode))
            {
                return new ReportSelection(effectiveReportCode, effectiveMenuCode);
            }

            if (string.IsNullOrWhiteSpace(effectiveReportGroupCode))
            {
                throw new ArgumentException("reportCode, menuCode, or reportGroupCode is required.");
            }

            var options = await _reportOptionRepository.GetReportOptionsAsync(
                companyCd,
                effectiveReportGroupCode,
                cancellationToken);
            var selectedOption = options
                .OrderBy(option => option.IS_DEFAULT == "1" ? 0 : 1)
                .ThenBy(option => option.SORT_ORDER)
                .FirstOrDefault();

            if (selectedOption == null || string.IsNullOrWhiteSpace(selectedOption.REPORT_CODE))
            {
                throw new KeyNotFoundException("Report option configuration not found.");
            }

            return new ReportSelection(selectedOption.REPORT_CODE.Trim(), selectedOption.REPORT_CODE.Trim());
        }


        private sealed record ReportSelection(string? ReportCode, string? MenuCode);
    }
}
