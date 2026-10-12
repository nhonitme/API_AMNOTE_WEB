using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Authorize]
[Route("api/ap/utility/check-service-allocation")]
public sealed class ServiceAllocationCheckController : BaseApiController
{
    private const string MenuCode = "AP_UTILITY_CHECK_ALLOC";
    private readonly IServiceAllocationCheckRepository _repository;

    public ServiceAllocationCheckController(IServiceAllocationCheckRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd,
        [FromQuery] long? customerId = null,
        [FromQuery] int? allocationFilter = null)
    {
        await EnsurePermissionAsync(MenuCode, "VIEW");

        if (!IsValidYmd(fromYmd) || !IsValidYmd(toYmd))
            return ValidationError("fromYmd and toYmd must have yyyyMMdd format.");

        if (string.CompareOrdinal(fromYmd, toYmd) > 0)
            return ValidationError("fromYmd must be less than or equal to toYmd.");

        if (customerId is < 0)
            return ValidationError("customerId must be positive.");

        if (allocationFilter.HasValue && allocationFilter != 0 && allocationFilter != 1)
            return ValidationError("allocationFilter must be null, 0 (allocated), or 1 (not allocated).");

        var result = await _repository.GetAsync(
            Common.GetCompanyCode(),
            fromYmd!,
            toYmd!,
            customerId,
            allocationFilter,
            Common.GetDatabaseName());

        return Success(result);
    }

    private static bool IsValidYmd(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
}
