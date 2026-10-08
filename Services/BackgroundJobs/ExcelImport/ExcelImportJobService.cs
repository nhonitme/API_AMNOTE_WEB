using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportJobService : IExcelImportJobService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xls"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IExcelImportJobQueue _queue;
    private readonly IExcelImportJobStore _jobStore;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExcelImportJobService(
        IWebHostEnvironment environment,
        IExcelImportJobQueue queue,
        IExcelImportJobStore jobStore,
        IHttpContextAccessor httpContextAccessor)
    {
        _environment = environment;
        _queue = queue;
        _jobStore = jobStore;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ExcelImportStartResultDto> StartAsync(
        IFormFile file,
        string moduleCd,
        string? userId,
        Dictionary<string, string?>? extraParams = null,
        CancellationToken cancellationToken = default)
    {
        var companyCd = Common.GetCompanyCode();
        var databaseName = Common.GetDatabaseName();
        var actualLang = Common.NormalizeLanguageCode(Common.GetCurrentLanguage());

        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_REQUIRED", actualLang));
        }

        if (string.IsNullOrWhiteSpace(moduleCd))
        {
            throw new InvalidOperationException(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_MODULE_REQUIRED", actualLang));
        }

        if (string.IsNullOrWhiteSpace(companyCd))
        {
            throw new InvalidOperationException(ExcelImportHandlerHelper.GetLocalizedMessage("COMPANY_CD_REQUIRED", actualLang));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_INVALID_FILE_EXTENSION", actualLang));
        }

        var jobId = Guid.NewGuid().ToString("N");
        var tempFolder = AmnoteRuntimePaths.CombineAppData(_environment.ContentRootPath, "excel-import-temp");
        Directory.CreateDirectory(tempFolder);

        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".xlsx" : extension;
        var tempFilePath = Path.Combine(tempFolder, $"{jobId}{safeExtension}");

        await using (var stream = File.Create(tempFilePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var request = new ExcelImportJobRequest
        {
            JobId = jobId,
            ModuleCd = moduleCd,
            CompanyCd = companyCd,
            DatabaseName = databaseName,
            Lang = actualLang,
            UserId = userId,
            UserIp = ClientPublicIpResolver.ResolveFromRequest(httpContext),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            OriginalFileName = file.FileName,
            TempFilePath = tempFilePath,
            Params = extraParams ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            CreatedAt = DateTime.Now
        };

        _jobStore.Create(request);
        await _queue.EnqueueAsync(request, cancellationToken);

        return new ExcelImportStartResultDto
        {
            JobId = request.JobId,
            Status = BackgroundJobStatus.Queued,
            Message = ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_QUEUED", actualLang)
        };
    }
}
