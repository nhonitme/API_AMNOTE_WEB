using API_AMNOTE_WEB.Models;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class ExcelImportJobRequest
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");
    public string ModuleCd { get; init; } = string.Empty;
    public string CompanyCd { get; init; } = string.Empty;
    public string? DatabaseName { get; init; }
    public string? Lang { get; init; }
    public string? UserId { get; init; }
    /// <summary>Captured at enqueue so background workers can write activity log with the real client IP.</summary>
    public string? UserIp { get; init; }
    public string? UserAgent { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string TempFilePath { get; init; } = string.Empty;
    public Dictionary<string, string?> Params { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

public sealed class ExcelImportStartResultDto
{
    public string JobId { get; init; } = string.Empty;
    public string Status { get; init; } = BackgroundJobStatus.Queued;
    public string Message { get; init; } = "Đã đưa file vào hàng đợi import.";
}

public sealed class ExcelImportProgressDto
{
    public string JobId { get; init; } = string.Empty;
    public string ModuleCd { get; init; } = string.Empty;
    public string Lang { get; init; } = "VIET";
    public string Status { get; init; } = BackgroundJobStatus.Queued;
    public int Percent { get; init; }
    public int ProcessedRows { get; init; }
    public int TotalRows { get; init; }
    public string? Message { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ExcelImportResultDto? Result { get; init; }
}

public sealed class ExcelImportResultDto
{
    public bool Success { get; init; }
    public int TotalRows { get; init; }
    public int SuccessRows { get; init; }
    public int WarningRows { get; init; }
    public int ErrorRows { get; init; }
    public string? Message { get; init; }
    public string? SheetName { get; init; }
    public List<ExcelImportResultRowDto> Rows { get; init; } = new();
}

public sealed class ExcelImportResultRowDto
{
    public int RowNo { get; init; }
    public string Status { get; init; } = "SUCCESS";
    public string Message { get; init; } = string.Empty;
    public string? SheetName { get; init; }
    public string? KeyValue { get; init; }
}

public sealed class ExcelImportJobState
{
    public string JobId { get; init; } = string.Empty;
    public string ModuleCd { get; init; } = string.Empty;
    public string Lang { get; init; } = "VIET";
    public string Status { get; set; } = BackgroundJobStatus.Queued;
    public int Percent { get; set; }
    public int ProcessedRows { get; set; }
    public int TotalRows { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public ExcelImportResultDto? Result { get; set; }

    [JsonIgnore]
    public string? TempFilePath { get; set; }
}
