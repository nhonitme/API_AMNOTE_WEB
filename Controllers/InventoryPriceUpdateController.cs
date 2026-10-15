using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory/price-update")]
public sealed class InventoryPriceUpdateController : BaseApiController
{
    private const string InputMenuCode = "INV_UPDATE_IN_PRICE";
    private const string OutputMenuCode = "INV_UPDATE_OUT_PRICE";
    private readonly InventoryPriceUpdateRepository _repository;

    public InventoryPriceUpdateController(InventoryPriceUpdateRepository repository) => _repository = repository;

    [HttpGet("in-price")]
    public Task<IActionResult> GetInputPrices([FromQuery] string? STORE_CD, [FromQuery] string? FROM_YMD, [FromQuery] string? TO_YMD)
        => GetRowsAsync("IN", InputMenuCode, STORE_CD, FROM_YMD, TO_YMD);

    [HttpGet("out-price")]
    public Task<IActionResult> GetOutputPrices([FromQuery] string? STORE_CD, [FromQuery] string? FROM_YMD, [FromQuery] string? TO_YMD)
        => GetRowsAsync("OUT", OutputMenuCode, STORE_CD, FROM_YMD, TO_YMD);

    [HttpPost("in-price")]
    public Task<IActionResult> ApplyInputPrices([FromBody] InventoryPriceUpdateRequest? request)
        => ApplyAsync("IN", InputMenuCode, request);

    [HttpPost("out-price")]
    public Task<IActionResult> ApplyOutputPrices([FromBody] InventoryPriceUpdateRequest? request)
        => ApplyAsync("OUT", OutputMenuCode, request);

    private async Task<IActionResult> GetRowsAsync(string flow, string menuCode, string? storeCd, string? fromYmd, string? toYmd)
    {
        await EnsurePermissionAsync(menuCode, "VIEW");
        try
        {
            var filter = NormalizeFilter(storeCd, fromYmd, toYmd);
            var rows = await _repository.GetRowsAsync(flow, Common.GetCompanyCode(), Common.GetDatabaseName(), filter.StoreCd, filter.FromYmd, filter.ToYmd);
            return Success(rows);
        }
        catch (ArgumentException ex) { return ValidationError(ex.Message); }
        catch (MySqlException ex) when (ex.SqlState == "45000") { return ValidationError(ex.Message); }
    }

    private async Task<IActionResult> ApplyAsync(string flow, string menuCode, InventoryPriceUpdateRequest? request)
    {
        await EnsurePermissionAsync(menuCode, "EDIT");
        if (request?.ROWS == null || request.ROWS.Count == 0) return ValidationError("At least one price row is required");
        try
        {
            var filter = NormalizeFilter(request.STORE_CD, request.FROM_YMD, request.TO_YMD);
            var rows = request.ROWS.ToList();
            if (rows.Any(row => row == null || string.IsNullOrWhiteSpace(row.PRODUCT_CD) || !row.NEW_UNIT_PRICE.HasValue || row.NEW_UNIT_PRICE.Value < 0))
                return ValidationError("Each row must have PRODUCT_CD and a non-negative NEW_UNIT_PRICE");
            if (rows.Select(row => row.PRODUCT_CD.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count)
                return ValidationError("Duplicate PRODUCT_CD rows are not allowed");
            foreach (var row in rows) row.PRODUCT_CD = row.PRODUCT_CD.Trim();
            await _repository.ApplyAsync(flow, Common.GetCompanyCode(), Common.GetDatabaseName(), Common.GetUserId(),
                filter.StoreCd, filter.FromYmd, filter.ToYmd, rows);
            return Success(new InventoryPriceUpdateResult { UPDATED_PRODUCTS = rows.Count }, "Prices updated successfully");
        }
        catch (ArgumentException ex) { return ValidationError(ex.Message); }
        catch (InvalidOperationException ex) { return ValidationError(ex.Message); }
        catch (MySqlException ex) when (ex.SqlState == "45000") { return ValidationError(ex.Message); }
    }

    private static (string StoreCd, string FromYmd, string ToYmd) NormalizeFilter(string? storeCd, string? fromYmd, string? toYmd)
    {
        var normalizedStore = Common.NormalizeNullableText(storeCd);
        var normalizedFrom = Common.NormalizeNullableYmdText(fromYmd, nameof(fromYmd));
        var normalizedTo = Common.NormalizeNullableYmdText(toYmd, nameof(toYmd));
        if (normalizedFrom == null || normalizedTo == null)
            throw new ArgumentException("FROM_YMD and TO_YMD are required");
        if (string.CompareOrdinal(normalizedFrom, normalizedTo) > 0)
            throw new ArgumentException("FROM_YMD must be less than or equal to TO_YMD");
        return (normalizedStore ?? string.Empty, normalizedFrom, normalizedTo);
    }
}
