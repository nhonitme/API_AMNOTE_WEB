using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Authorize]
[Route("api/ap/utility/offset")]
public sealed class DebtOffsetController : BaseApiController
{
    // Must match MENU_CD granted to the AP utility route in the manager database.
    private const string MenuCode = "AP_UTILITY_OFFSET";
    private readonly DebtOffsetRepository _repo;

    public DebtOffsetController(DebtOffsetRepository repo) => _repo = repo;

    private static bool ValidKind(string? kind) =>
        kind is "PAYMENT" or "COLLECT" or "CLEARING";

    private static bool ValidYmd(string? ymd) =>
        DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool ValidCustomerIds(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "ALL" ||
        Regex.IsMatch(value, @"^[0-9]+(,[0-9]+)*$");

    private static bool ValidCurrencyFilter(string? value) =>
        string.IsNullOrEmpty(value) ||
        Regex.IsMatch(value, @"^([12]\|)?(VND|USD|EUR|JPY|KRW|CNY)$");

    [HttpGet("available")]
    public async Task<IActionResult> Available(
        [FromQuery] string? kind, [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd, [FromQuery] string? customerIds = "ALL",
        [FromQuery] string? currencyFilter = "VND")
    {
        await EnsurePermissionAsync(MenuCode, "VIEW");
        if (!ValidKind(kind) || !ValidYmd(fromYmd) || !ValidYmd(toYmd) ||
            string.CompareOrdinal(fromYmd, toYmd) > 0 ||
            !ValidCustomerIds(customerIds) || !ValidCurrencyFilter(currencyFilter))
            return ValidationError("Invalid debt offset filters");
        var data = await _repo.AvailableAsync(Common.GetCompanyCode(), kind!, fromYmd!, toYmd!,
            string.IsNullOrWhiteSpace(customerIds) ? "ALL" : customerIds!, currencyFilter ?? "", Common.GetDatabaseName());
        return Success(data);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] string? kind, [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd, [FromQuery] string? customerIds = "ALL",
        [FromQuery] string? currencyFilter = "VND")
    {
        await EnsurePermissionAsync(MenuCode, "VIEW");
        if (!ValidKind(kind) || !ValidYmd(fromYmd) || !ValidYmd(toYmd) ||
            string.CompareOrdinal(fromYmd, toYmd) > 0 ||
            !ValidCustomerIds(customerIds) || !ValidCurrencyFilter(currencyFilter))
            return ValidationError("Invalid debt offset history filters");
        return Success(await _repo.HistoryAsync(Common.GetCompanyCode(), kind!, fromYmd!, toYmd!,
            string.IsNullOrWhiteSpace(customerIds) ? "ALL" : customerIds!, currencyFilter ?? "", Common.GetDatabaseName()));
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] DebtOffsetSaveRequest? request)
    {
        await EnsurePermissionAsync(MenuCode, "ADD");
        if (request == null || !ValidKind(request.Kind) || request.CustomerId <= 0 ||
            request.Amount < 0 || request.AmountFc < 0 ||
            (request.Amount == 0 && request.AmountFc == 0) ||
            !Regex.IsMatch(request.Currency ?? "", @"^(VND|USD|EUR|JPY|KRW|CNY)$"))
            return ValidationError("Invalid debt offset data");

        bool validSource = request.SourceChitId > 0 ||
            request.SourceChitId == (request.Kind == "PAYMENT" ? -331 : -131);
        bool validDebt = request.DebtChitId > 0 ||
            request.DebtChitId == (request.Kind == "COLLECT" ? -131 : -331);
        if (!validSource || !validDebt || (request.Currency == "VND" && request.AmountFc != 0))
            return ValidationError("Invalid voucher identity or currency");

        try
        {
            // One request = one pair. For clearing, SQL writes both mirror rows in a single transaction.
            return Success(await _repo.SaveAsync(Common.GetCompanyCode(), Common.GetUserId(), request, Common.GetDatabaseName()));
        }
        catch (DbException ex)
        {
            return ValidationError(ex.Message);
        }
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DebtOffsetDeleteRequest? request)
    {
        await EnsurePermissionAsync(MenuCode, "DELETE");
        if (request == null || !ValidKind(request.Kind) || request.RecordId <= 0)
            return ValidationError("Invalid allocation ID");
        try
        {
            var deleted = await _repo.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(),
                request.Kind!, request.RecordId, Common.GetDatabaseName());
            return deleted == 1 ? Success(new { DELETED_COUNT = deleted }) : NotFound("Allocation already removed or not found");
        }
        catch (DbException ex)
        {
            return ValidationError(ex.Message);
        }
    }
}
