using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public interface IExcelImportJobService
{
    Task<ExcelImportStartResultDto> StartAsync(
        IFormFile file,
        string moduleCd,
        string? userId,
        Dictionary<string, string?>? extraParams = null,
        CancellationToken cancellationToken = default);
}
