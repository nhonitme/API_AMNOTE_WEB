using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Authorize]
[Route("api/excel-import-jobs")]
public sealed class ExcelImportJobController : BaseApiController
{
    private readonly IExcelImportJobService _jobService;
    private readonly IExcelImportJobStore _jobStore;
    private readonly IExcelImportJobCancellationRegistry _cancellationRegistry;

    public ExcelImportJobController(
        IExcelImportJobService jobService,
        IExcelImportJobStore jobStore,
        IExcelImportJobCancellationRegistry cancellationRegistry)
    {
        _jobService = jobService;
        _jobStore = jobStore;
        _cancellationRegistry = cancellationRegistry;
    }

    [HttpPost("start")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    public async Task<IActionResult> Start(
        IFormFile file,
        [FromForm] string moduleCd,
        CancellationToken cancellationToken)
    {
        var userId = Common.GetUserId();

        var extraParams = Request.Form.Keys
            .Where(key => !IsSystemFormKey(key))
            .ToDictionary(key => key, key => Request.Form[key].FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

        var result = await _jobService.StartAsync(
            file,
            moduleCd,
            userId,
            extraParams,
            cancellationToken);

        return Success(result);
    }

    private static bool IsSystemFormKey(string key)
    {
        return string.Equals(key, "file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "moduleCd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "screenCd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "companyCd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "COMPANY_CD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "databaseName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "DATABASE_NAME", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "dbName", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "DB_NAME", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "lang", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("progress")]
    public IActionResult GetProgress([FromQuery] string jobId)
    {
        var progress = _jobStore.GetProgress(jobId);
        if (progress == null)
        {
            var lang = Common.GetCurrentLanguage();
            return NotFound(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_PROGRESS_NOT_FOUND", lang));
        }

        return Success(progress);
    }

    /// <summary>
    /// Explicit user Cancel Import only. Closing browser / F5 / logout must NOT call this.
    /// </summary>
    [HttpPost("cancel")]
    public IActionResult Cancel([FromQuery] string jobId)
    {
        var lang = Common.GetCurrentLanguage();
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_CANCEL_NOT_FOUND", lang));
        }

        var message = ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_CANCELLED", lang);
        var marked = _jobStore.TryCancel(jobId, message);
        _cancellationRegistry.TryCancel(jobId);

        var progress = _jobStore.GetProgress(jobId);
        if (progress == null)
        {
            return NotFound(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_CANCEL_NOT_FOUND", lang));
        }

        return Success(new
        {
            cancelled = marked || string.Equals(progress.Status, BackgroundJobStatus.Cancelled, StringComparison.OrdinalIgnoreCase),
            progress,
        });
    }
}
