using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.Shared;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

/// <summary>
/// In-memory job store with App_Data file mirror so progress survives process recycle
/// and multi-worker instances sharing the same ContentRoot.
/// </summary>
public sealed class ExcelImportJobStore : IExcelImportJobStore
{
    private static readonly TimeSpan ProgressPersistMinInterval = TimeSpan.FromMilliseconds(800);
    private const int PersistResultRowCap = 80;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ConcurrentDictionary<string, ExcelImportJobState> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _lastProgressPersistTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _persistDir;
    private readonly bool _diskPersistEnabled;
    private readonly object _ioGate = new();

    public ExcelImportJobStore(IWebHostEnvironment environment)
    {
        _persistDir = AmnoteRuntimePaths.CombineAppData(environment.ContentRootPath, "excel-import-jobs");
        // Memory-only on macOS/Linux: disk persist + orphan "API đã restart" ERROR made convert
        // fail even after jobs moved to /tmp, whenever the process still recycled (e.g. SMB logs).
        _diskPersistEnabled = !(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            || string.Equals(
                Environment.GetEnvironmentVariable("AMNOTE_EXCEL_JOB_PERSIST"),
                "1",
                StringComparison.OrdinalIgnoreCase);
        try
        {
            if (_diskPersistEnabled)
            {
                Directory.CreateDirectory(_persistDir);
                RecoverOrphansOnStartup();
            }
            else
            {
                TryDeleteUnfinishedPersistedJobs();
            }
        }
        catch
        {
            // Persist dir unavailable — keep memory-only store.
        }
    }

    public ExcelImportJobState Create(ExcelImportJobRequest request)
    {
        var lang = string.IsNullOrWhiteSpace(request.Lang) ? Common.GetCurrentLanguage() : request.Lang;
        var state = new ExcelImportJobState
        {
            JobId = request.JobId,
            ModuleCd = request.ModuleCd,
            Lang = lang,
            Status = BackgroundJobStatus.Queued,
            Percent = 0,
            ProcessedRows = 0,
            TotalRows = 0,
            Message = ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_QUEUED", lang),
            CreatedAt = request.CreatedAt,
            UpdatedAt = DateTime.Now,
            TempFilePath = request.TempFilePath
        };

        _jobs[state.JobId] = state;
        Persist(state, force: true);
        return state;
    }

    public int CleanupCompletedJobs(TimeSpan keepDuration)
    {
        var removed = BackgroundJobMemoryCleanup.Cleanup(
            _jobs,
            keepDuration,
            job => BackgroundJobStatus.IsFinished(job.Status),
            job => job.UpdatedAt ?? job.CreatedAt);

        TryCleanupPersistedFiles(keepDuration);
        return removed;
    }

    public bool HasCleanupCandidates(TimeSpan keepDuration)
    {
        return BackgroundJobMemoryCleanup.HasCleanupCandidates(
            _jobs,
            keepDuration,
            job => BackgroundJobStatus.IsFinished(job.Status),
            job => job.UpdatedAt ?? job.CreatedAt);
    }

    public ExcelImportJobState? Get(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        if (_jobs.TryGetValue(jobId, out var state))
        {
            return state;
        }

        var fromDisk = TryLoadFromDisk(jobId);
        if (fromDisk != null)
        {
            _jobs[jobId] = fromDisk;
        }

        return fromDisk;
    }

    public ExcelImportProgressDto? GetProgress(string jobId)
    {
        var state = Get(jobId);
        if (state == null)
        {
            return null;
        }

        return new ExcelImportProgressDto
        {
            JobId = state.JobId,
            ModuleCd = state.ModuleCd,
            Lang = state.Lang,
            Status = state.Status,
            Percent = state.Percent,
            ProcessedRows = state.ProcessedRows,
            TotalRows = state.TotalRows,
            Message = state.Message,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt,
            Result = state.Result
        };
    }

    public void MarkProcessing(string jobId, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            state = TryLoadFromDisk(jobId);
            if (state == null)
            {
                return;
            }

            _jobs[jobId] = state;
        }

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.Message = message ?? ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_PROCESSING", state.Lang);
        state.UpdatedAt = DateTime.Now;
        Persist(state, force: true);
    }

    public void UpdateProgress(string jobId, int processedRows, int totalRows, string? message = null, int? percent = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            state = TryLoadFromDisk(jobId);
            if (state == null)
            {
                return;
            }

            _jobs[jobId] = state;
        }

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.ProcessedRows = Math.Max(0, processedRows);
        state.TotalRows = Math.Max(0, totalRows);
        state.Percent = percent.HasValue
            ? Math.Clamp(percent.Value, 0, 100)
            : BackgroundJobProgressHelper.CalculatePercent(processedRows, totalRows, state.Percent, 0, 99);
        state.Message = message;
        state.UpdatedAt = DateTime.Now;
        // Memory only during progress — disk write every ~800ms under ContentRoot/App_Data was
        // correlated with Mac process recycle (~9s) while the share folder is watched/deployed.
        // Create / Complete / Fail / MarkProcessing still Persist(force: true).
    }

    public void UpdatePercent(string jobId, int percent, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state)) return;

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return;
        }

        state.Status = BackgroundJobStatus.Processing;
        state.Percent = Math.Clamp(percent, 0, 100);
        if (message != null)
        {
            state.Message = message;
        }
        state.UpdatedAt = DateTime.Now;
    }

    public void Complete(string jobId, ExcelImportResultDto result)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            state = TryLoadFromDisk(jobId);
            if (state == null)
            {
                return;
            }

            _jobs[jobId] = state;
        }

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return;
        }

        state.Status = result.Success ? BackgroundJobStatus.Done : BackgroundJobStatus.Error;
        state.Percent = 100;
        state.ProcessedRows = result.TotalRows;
        state.TotalRows = result.TotalRows;
        state.Message = result.Message;
        state.Result = CapResultRows(result);
        state.UpdatedAt = DateTime.Now;
        Persist(state, force: true);
    }

    public void Fail(string jobId, string message)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            state = TryLoadFromDisk(jobId);
            if (state == null)
            {
                return;
            }

            _jobs[jobId] = state;
        }

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return;
        }

        var rowNo = TryParseExcelRowNo(message);
        state.Status = BackgroundJobStatus.Error;
        state.Percent = 100;
        state.Message = message;
        state.Result = new ExcelImportResultDto
        {
            Success = false,
            TotalRows = state.TotalRows,
            SuccessRows = 0,
            WarningRows = 0,
            ErrorRows = 1,
            Message = message,
            Rows = new List<ExcelImportResultRowDto>
            {
                new()
                {
                    RowNo = rowNo,
                    Status = "ERROR",
                    Message = message
                }
            }
        };
        state.UpdatedAt = DateTime.Now;
        Persist(state, force: true);
    }

    public bool TryCancel(string jobId, string? message = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
        {
            state = TryLoadFromDisk(jobId);
            if (state == null)
            {
                return false;
            }

            _jobs[jobId] = state;
        }

        if (BackgroundJobStatus.IsFinished(state.Status))
        {
            return false;
        }

        var cancelMessage = string.IsNullOrWhiteSpace(message)
            ? ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_CANCELLED", state.Lang)
            : message;

        state.Status = BackgroundJobStatus.Cancelled;
        state.Message = cancelMessage;
        state.Result = new ExcelImportResultDto
        {
            Success = false,
            TotalRows = state.TotalRows,
            SuccessRows = 0,
            WarningRows = 0,
            ErrorRows = 0,
            Message = cancelMessage,
            Rows = new List<ExcelImportResultRowDto>(),
        };
        state.UpdatedAt = DateTime.Now;
        Persist(state, force: true);
        return true;
    }

    private void RecoverOrphansOnStartup()
    {
        string[] files;
        try
        {
            if (!Directory.Exists(_persistDir))
            {
                return;
            }

            files = Directory.GetFiles(_persistDir, "*.json");
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                var state = JsonSerializer.Deserialize<ExcelImportJobState>(File.ReadAllText(file), JsonOptions);
                if (state == null || string.IsNullOrWhiteSpace(state.JobId))
                {
                    continue;
                }

                if (!BackgroundJobStatus.IsFinished(state.Status))
                {
                    // Drop in-flight jobs silently — surfacing "API đã restart" ERROR made convert
                    // treat a recycle as a finished failure instead of 404 → retry.
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore
                    }

                    continue;
                }

                _jobs[state.JobId] = state;
            }
            catch
            {
                // skip corrupt file
            }
        }
    }

    private void TryDeleteUnfinishedPersistedJobs()
    {
        try
        {
            if (!Directory.Exists(_persistDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(_persistDir, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private void Persist(ExcelImportJobState state, bool force)
    {
        if (!_diskPersistEnabled)
        {
            return;
        }

        if (!force)
        {
            var now = Environment.TickCount64;
            if (_lastProgressPersistTicks.TryGetValue(state.JobId, out var last)
                && now - last < ProgressPersistMinInterval.TotalMilliseconds)
            {
                return;
            }

            _lastProgressPersistTicks[state.JobId] = now;
        }
        else
        {
            _lastProgressPersistTicks[state.JobId] = Environment.TickCount64;
        }

        try
        {
            lock (_ioGate)
            {
                WriteDiskUnlocked(state);
            }
        }
        catch
        {
            // Disk persist is best-effort; memory state remains source of truth.
        }
    }

    private void WriteDiskUnlocked(ExcelImportJobState state)
    {
        Directory.CreateDirectory(_persistDir);
        var path = GetPath(state.JobId);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(tmp, json);
        File.Copy(tmp, path, overwrite: true);
        try
        {
            File.Delete(tmp);
        }
        catch
        {
            // ignore
        }
    }

    private ExcelImportJobState? TryLoadFromDisk(string jobId)
    {
        if (!_diskPersistEnabled)
        {
            return null;
        }

        try
        {
            var path = GetPath(jobId);
            if (!File.Exists(path))
            {
                return null;
            }

            lock (_ioGate)
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<ExcelImportJobState>(File.ReadAllText(path), JsonOptions);
            }
        }
        catch
        {
            return null;
        }
    }

    private void TryCleanupPersistedFiles(TimeSpan keepDuration)
    {
        try
        {
            var cutoff = DateTime.Now - keepDuration;
            foreach (var file in Directory.GetFiles(_persistDir, "*.json"))
            {
                try
                {
                    var state = JsonSerializer.Deserialize<ExcelImportJobState>(File.ReadAllText(file), JsonOptions);
                    if (state == null || !BackgroundJobStatus.IsFinished(state.Status))
                    {
                        continue;
                    }

                    var stamp = state.UpdatedAt ?? state.CreatedAt;
                    if (stamp < cutoff)
                    {
                        File.Delete(file);
                        _jobs.TryRemove(state.JobId, out _);
                        _lastProgressPersistTicks.TryRemove(state.JobId, out _);
                    }
                }
                catch
                {
                    // skip
                }
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private string GetPath(string jobId)
    {
        var safe = Path.GetFileName(jobId.Trim());
        return Path.Combine(_persistDir, $"{safe}.json");
    }

    private static ExcelImportResultDto CapResultRows(ExcelImportResultDto result)
    {
        var rows = result.Rows ?? new List<ExcelImportResultRowDto>();
        if (rows.Count <= PersistResultRowCap)
        {
            return result;
        }

        return new ExcelImportResultDto
        {
            Success = result.Success,
            TotalRows = result.TotalRows,
            SuccessRows = result.SuccessRows,
            WarningRows = result.WarningRows,
            ErrorRows = result.ErrorRows,
            Message = result.Message,
            SheetName = result.SheetName,
            Rows = rows.Take(PersistResultRowCap).ToList()
        };
    }

    /// <summary>
    /// Prefer explicit Excel row from messages like "Row 3: ..." (1-based sheet row).
    /// </summary>
    private static int TryParseExcelRowNo(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return 0;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            message,
            @"^\s*Row\s+(\d+)\s*:",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return match.Success && int.TryParse(match.Groups[1].Value, out var rowNo) && rowNo > 0
            ? rowNo
            : 0;
    }
}
