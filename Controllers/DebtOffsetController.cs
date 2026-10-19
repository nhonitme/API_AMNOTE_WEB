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

    // An empty From date means "from the beginning", as used by the optional report filter.
    private static bool ValidOptionalFromYmd(string? ymd) =>
        string.IsNullOrEmpty(ymd) || ValidYmd(ymd);

    private static bool ValidCustomerIds(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "ALL" ||
        Regex.IsMatch(value, @"^[0-9]+(,[0-9]+)*$");

    // Optional 1| or 2| keeps the existing foreign-currency filter mode. Any currency code is allowed.
    private static bool ValidCurrencyCode(string? value) =>
        !string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^[A-Za-z0-9]{1,20}$");

    private static bool ValidCurrencyFilter(string? value) =>
        string.IsNullOrEmpty(value) ||
        Regex.IsMatch(value, @"^([12]\|)?[A-Za-z0-9]{1,20}$");

    private static bool ValidAccountCode(string? value) =>
        string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value, @"^[0-9]{3,20}$");

    [HttpGet("available")]
    public async Task<IActionResult> Available(
        [FromQuery] string? kind, [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd, [FromQuery] string? customerIds = "ALL",
        [FromQuery] string? currencyFilter = "VND",
        [FromQuery] string? accCd = null, [FromQuery] string? accCd1 = null,
        [FromQuery] string? accCd2 = null)
    {
        await EnsurePermissionAsync(MenuCode, "VIEW");
        if (!ValidKind(kind) || !ValidOptionalFromYmd(fromYmd) || !ValidYmd(toYmd) ||
            (!string.IsNullOrEmpty(fromYmd) && string.CompareOrdinal(fromYmd, toYmd) > 0) ||
            !ValidCustomerIds(customerIds) || !ValidCurrencyFilter(currencyFilter) ||
            !ValidAccountCode(accCd) || !ValidAccountCode(accCd1) || !ValidAccountCode(accCd2))
            return ValidationError("Invalid debt offset filters");

        // Matching account determines receivable/payable direction, never a stale UI mode.
        string selectedAccount = string.IsNullOrWhiteSpace(accCd) ? (kind == "COLLECT" ? "131" : "331") : accCd.Trim();
        string selectedAccount1 = string.IsNullOrWhiteSpace(accCd1) ? "131" : accCd1.Trim();
        string selectedAccount2 = string.IsNullOrWhiteSpace(accCd2) ? "331" : accCd2.Trim();
        if (kind != "CLEARING" &&
            (kind == "COLLECT" ? !selectedAccount.StartsWith("131") : !selectedAccount.StartsWith("331")))
            return ValidationError("Account does not match debt direction");
        if (kind == "CLEARING" &&
            (!selectedAccount1.StartsWith("131") || !selectedAccount2.StartsWith("331")))
            return ValidationError("Clearing requires receivable 131* and payable 331* accounts");
        var data = await _repo.AvailableAsync(Common.GetCompanyCode(), kind!, fromYmd ?? string.Empty, toYmd!,
            string.IsNullOrWhiteSpace(customerIds) ? "ALL" : customerIds!, currencyFilter ?? "",
            selectedAccount, selectedAccount1, selectedAccount2, Common.GetDatabaseName());
        return Success(data);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] string? kind, [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd, [FromQuery] string? customerIds = "ALL",
        [FromQuery] string? currencyFilter = "VND",
        [FromQuery] string? accCd = null)
    {
        await EnsurePermissionAsync(MenuCode, "VIEW");
        if (!ValidKind(kind) || !ValidOptionalFromYmd(fromYmd) || !ValidYmd(toYmd) ||
            (!string.IsNullOrEmpty(fromYmd) && string.CompareOrdinal(fromYmd, toYmd) > 0) ||
            !ValidCustomerIds(customerIds) || !ValidCurrencyFilter(currencyFilter) ||
            !ValidAccountCode(accCd))
            return ValidationError("Invalid debt offset history filters");
        return Success(await _repo.HistoryAsync(Common.GetCompanyCode(), kind!, fromYmd ?? string.Empty, toYmd!,
            string.IsNullOrWhiteSpace(customerIds) ? "ALL" : customerIds!, currencyFilter ?? "",
            string.IsNullOrWhiteSpace(accCd) ? (kind == "COLLECT" ? "131" : "331") : accCd.Trim(),
            Common.GetDatabaseName()));
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] DebtOffsetSaveRequest? request)
    {
        await EnsurePermissionAsync(MenuCode, "ADD");
        if (request == null || !ValidKind(request.Kind) || request.CustomerId <= 0 ||
            request.Amount < 0 || request.AmountFc < 0 ||
            (request.Amount == 0 && request.AmountFc == 0) ||
            !ValidCurrencyCode(request.Currency))
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
