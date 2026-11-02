using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers;

/// <summary>
/// G-K Giá thành giản đơn — API đọc (2C-01). COMPANY_CD lấy từ auth, không tin FE.
/// </summary>
[ApiController]
[Authorize]
[Route("api/production-cost")]
public sealed class ProductionCostController : BaseApiController
{
    private readonly IProductionCostService _service;
    private readonly ILogger<ProductionCostController> _logger;

    public ProductionCostController(IProductionCostService service, ILogger<ProductionCostController> logger)
    {
        _service = service;
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

    private static ProductionCostQuery Query(string? periodYm, long? runId, int? runVersion) =>
        new()
        {
            PeriodYm = periodYm ?? string.Empty,
            RunId = runId,
            RunVersion = runVersion
        };

    private static string CompanyCd() => Common.GetCompanyCode();

    /// <summary>
    /// DB công ty bắt buộc từ auth — không nuốt lỗi rồi fallback sang DB không xác định.
    /// </summary>
    private static string DbName() => Common.GetDatabaseName();

    private async Task<IActionResult> ReadAsync<T>(Func<Task<T>> action)
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
            _logger.LogWarning(ex, "ProductionCost read missing auth/company/database context");
            return BusinessError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ProductionCost read invalid operation");
            return BusinessError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProductionCost read failed");
            return ServerError("Không đọc được dữ liệu giá thành giản đơn.");
        }
    }
}
