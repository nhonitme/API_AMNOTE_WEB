using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers;

/// <summary>
/// G-K Giá thành giản đơn — API đọc (2C-01) + ghi Bước 2/3 (2C-02, mặc định chặn).
/// COMPANY_CD lấy từ auth, không tin FE.
/// </summary>
[ApiController]
[Authorize]
[Route("api/production-cost")]
public sealed class ProductionCostController : BaseApiController
{
    private readonly IProductionCostService _service;
    private readonly IProductionCostWriteService _writeService;
    private readonly ILogger<ProductionCostController> _logger;

    public ProductionCostController(
        IProductionCostService service,
        IProductionCostWriteService writeService,
        ILogger<ProductionCostController> logger)
    {
        _service = service;
        _writeService = writeService;
        _logger = logger;
    }

    /// <summary>GET /api/production-cost/period?periodYm=yyyyMM</summary>
    [HttpGet("period")]
    public async Task<IActionResult> GetPeriod([FromQuery] string? periodYm)
    {
        return await ReadAsync(() => _service.GetPeriodAsync(CompanyCd(), periodYm ?? string.Empty, DbName()));
    }

    /// <summary>GET /api/production-cost/steps?periodYm=&amp;runId=&amp;runVersion=</summary>
    [HttpGet("steps")]
    public async Task<IActionResult> GetSteps(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetStepsAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/objects</summary>
    [HttpGet("objects")]
    public async Task<IActionResult> GetObjects(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetObjectsAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/allocation/pool</summary>
    [HttpGet("allocation/pool")]
    public async Task<IActionResult> GetAllocPool(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetAllocPoolAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/allocation/basis</summary>
    [HttpGet("allocation/basis")]
    public async Task<IActionResult> GetAllocBasis(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetAllocBasisAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/allocation/detail</summary>
    [HttpGet("allocation/detail")]
    public async Task<IActionResult> GetAllocDetail(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetAllocDetailAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/wip</summary>
    [HttpGet("wip")]
    public async Task<IActionResult> GetWip(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetWipAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/receipts</summary>
    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetReceiptsAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/history</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? periodYm,
        [FromQuery] long? runId = null,
        [FromQuery] int? runVersion = null)
    {
        return await ReadAsync(() => _service.GetHistoryAsync(CompanyCd(), Query(periodYm, runId, runVersion), DbName()));
    }

    /// <summary>GET /api/production-cost/comparison</summary>
    [HttpGet("comparison")]
    public async Task<IActionResult> GetComparison(
        [FromQuery] string? periodYm,
        [FromQuery] long? baseRunId = null,
        [FromQuery] int? baseRunVersion = null,
        [FromQuery] long? compareRunId = null,
        [FromQuery] int? compareRunVersion = null)
    {
        return await ReadAsync(() => _service.GetComparisonAsync(
            CompanyCd(),
            periodYm ?? string.Empty,
            baseRunId,
            baseRunVersion,
            compareRunId,
            compareRunVersion,
            DbName()));
    }

    // ---- 2C-02 write (mặc định chặn ghi DB) ----

    /// <summary>POST /api/production-cost/period/ensure</summary>
    [HttpPost("period/ensure")]
    public async Task<IActionResult> EnsurePeriod([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.EnsureDraftRunAsync(
            CompanyCd(), UserIdOrNull(), request?.PeriodYm ?? string.Empty, DbName()));
    }

    /// <summary>PUT /api/production-cost/run/config</summary>
    [HttpPut("run/config")]
    public async Task<IActionResult> UpdateRunConfig([FromBody] ProductionCostRunConfigUpdate? request)
    {
        return await WriteAsync(async () =>
        {
            await _writeService.UpdateRunConfigAsync(CompanyCd(), UserIdOrNull(), request ?? new ProductionCostRunConfigUpdate(), DbName());
            return new { Saved = true };
        });
    }

    /// <summary>PUT /api/production-cost/objects/selection</summary>
    [HttpPut("objects/selection")]
    public async Task<IActionResult> UpdateObjectSelection([FromBody] ProductionCostObjectSelectionUpdate? request)
    {
        return await WriteAsync(async () =>
        {
            await _writeService.UpdateObjectSelectionAsync(CompanyCd(), UserIdOrNull(), request ?? new ProductionCostObjectSelectionUpdate(), DbName());
            return new { Saved = true };
        });
    }

    /// <summary>POST /api/production-cost/objects/sync-receipts</summary>
    [HttpPost("objects/sync-receipts")]
    public async Task<IActionResult> SyncObjects([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.SyncObjectsFromReceiptsAsync(
            CompanyCd(),
            UserIdOrNull(),
            request?.RunId ?? 0,
            request?.RunVersion ?? 0,
            DbName()));
    }

    /// <summary>PUT /api/production-cost/allocation/reduction</summary>
    [HttpPut("allocation/reduction")]
    public async Task<IActionResult> UpdateReduction([FromBody] ProductionCostPoolReductionUpdate? request)
    {
        return await WriteAsync(async () =>
        {
            await _writeService.UpdatePoolReductionAsync(CompanyCd(), UserIdOrNull(), request ?? new ProductionCostPoolReductionUpdate(), DbName());
            return new { Saved = true };
        });
    }

    /// <summary>PUT /api/production-cost/wip/adjustment</summary>
    [HttpPut("wip/adjustment")]
    public async Task<IActionResult> SaveWipAdjustment([FromBody] ProductionCostWipAdjustmentUpdate? request)
    {
        return await WriteAsync(async () =>
        {
            await _writeService.SaveWipAdjustmentAsync(CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWipAdjustmentUpdate(), DbName());
            return new { Saved = true };
        });
    }

    /// <summary>POST /api/production-cost/allocation/preview</summary>
    [HttpPost("allocation/preview")]
    public async Task<IActionResult> PreviewAllocation([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewAllocationAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/allocation/confirm</summary>
    [HttpPost("allocation/confirm")]
    public async Task<IActionResult> ConfirmAllocation([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.ConfirmAllocationAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/transfer/preview</summary>
    [HttpPost("transfer/preview")]
    public async Task<IActionResult> PreviewTransfer([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewTransferAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/transfer/confirm</summary>
    [HttpPost("transfer/confirm")]
    public async Task<IActionResult> ConfirmTransfer([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.ConfirmTransferAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    // ---- 2C-03 write (mặc định chặn ghi DB / không cập nhật giá kho) ----

    /// <summary>POST /api/production-cost/cost-calc/preview</summary>
    [HttpPost("cost-calc/preview")]
    public async Task<IActionResult> PreviewCostCalc([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewCostCalcAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/cost-calc/confirm</summary>
    [HttpPost("cost-calc/confirm")]
    public async Task<IActionResult> ConfirmCostCalc([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.ConfirmCostCalcAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    // ---- 2C-04 VALUATION (WriteGate OFF — không cập nhật kho) ----

    /// <summary>POST /api/production-cost/valuation/preview</summary>
    [HttpPost("valuation/preview")]
    public async Task<IActionResult> PreviewValuation([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewValuationAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/valuation/confirm</summary>
    [HttpPost("valuation/confirm")]
    public async Task<IActionResult> ConfirmValuation([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.ConfirmValuationAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    // ---- 2C-05 REOPEN / RECALCULATE / COMPARE (WriteGate OFF trên Confirm) ----

    /// <summary>POST /api/production-cost/reopen/preview</summary>
    [HttpPost("reopen/preview")]
    public async Task<IActionResult> PreviewReopen([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewReopenAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/reopen/confirm</summary>
    [HttpPost("reopen/confirm")]
    public async Task<IActionResult> ConfirmReopen([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.ConfirmReopenAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/recalculate/preview</summary>
    [HttpPost("recalculate/preview")]
    public async Task<IActionResult> PreviewRecalculate([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewRecalculateAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    /// <summary>POST /api/production-cost/compare/preview</summary>
    [HttpPost("compare/preview")]
    public async Task<IActionResult> PreviewCompare([FromBody] ProductionCostWriteRequest? request)
    {
        return await WriteAsync(() => _writeService.PreviewCompareAsync(
            CompanyCd(), UserIdOrNull(), request ?? new ProductionCostWriteRequest(), DbName()));
    }

    private static ProductionCostQuery Query(string? periodYm, long? runId, int? runVersion) =>
        new()
        {
            PeriodYm = periodYm ?? string.Empty,
            RunId = runId,
            RunVersion = runVersion
        };

    private static string CompanyCd() => Common.GetCompanyCode();

    private static string? UserIdOrNull()
    {
        try { return Common.GetUserId(); }
        catch { return null; }
    }

    /// <summary>
    /// DB công ty bắt buộc từ auth — không nuốt lỗi rồi fallback sang DB không xác định.
    /// </summary>
    private static string DbName() => Common.GetDatabaseName();

    private async Task<IActionResult> ReadAsync<T>(Func<Task<T>> action) => await ExecuteAsync(action, "read");

    private async Task<IActionResult> WriteAsync<T>(Func<Task<T>> action) => await ExecuteAsync(action, "write");

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action, string kind)
    {
        try
        {
            var data = await action();
            return Success(data);
        }
        catch (ArgumentException ex)
        {
            return ValidationError(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "ProductionCost {Kind} missing auth/company/database context", kind);
            return BusinessError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ProductionCost {Kind} invalid operation", kind);
            return BusinessError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProductionCost {Kind} failed", kind);
            return ServerError(kind == "write"
                ? "Không ghi được dữ liệu giá thành giản đơn."
                : "Không đọc được dữ liệu giá thành giản đơn.");
        }
    }
}
