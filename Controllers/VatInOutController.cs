using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers;

[Authorize]
[ApiController]
public sealed class VatInOutController : BaseApiController
{
    private readonly IVatInOutRepository _repository;
    public VatInOutController(IVatInOutRepository repository)
    {
        _repository = repository;
    }

    private static void Normalize(VatInOutQuery filter)
    {
        filter.TYPE = (filter.TYPE ?? "BUY").Trim().ToUpperInvariant();
        if (filter.TYPE != "BUY" && filter.TYPE != "SELL") throw new ArgumentException("TYPE must be BUY or SELL");
        filter.FROM_YMD = Common.NormalizeNullableYmdText(filter.FROM_YMD, nameof(filter.FROM_YMD));
        filter.TO_YMD = Common.NormalizeNullableYmdText(filter.TO_YMD, nameof(filter.TO_YMD));
        Common.ValidateYmdRange(filter.FROM_YMD, filter.TO_YMD);
        filter.STATUS = Common.NormalizeNullableText(filter.STATUS);
        filter.SEARCH_TEXT = Common.NormalizeNullableText(filter.SEARCH_TEXT);
        filter.LANGUAGE = Common.NormalizeLanguageCode(filter.LANGUAGE ?? Common.GetCurrentLanguage());
    }

    [HttpGet("Get")]
    public async Task<IActionResult> Get([FromQuery] VatInOutQuery filter,
        [FromQuery] int PAGE_NUMBER = 1, [FromQuery] int PAGE_SIZE = 20)
    {
        if (PAGE_NUMBER <= 0) return ValidationError("PAGE_NUMBER must be greater than 0");
        if (PAGE_SIZE <= 0) return ValidationError("PAGE_SIZE must be greater than 0");
        await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "VIEW");
        Normalize(filter);
        var (pageNumber, pageSize) = Common.ResolvePaging(null, PAGE_NUMBER, PAGE_SIZE);
        var result = await _repository.GetPagedAsync(Common.GetCompanyCode(), filter,
            pageNumber, pageSize, HttpContext.RequestAborted);
        return PagedSuccess(result.Items, pageNumber, pageSize, result.TotalRecords);
    }

    [HttpGet("ExportData")]
    public async Task<IActionResult> ExportData([FromQuery] VatInOutQuery filter)
    {
        await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "VIEW");
        await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EXPORT");
        Normalize(filter);
        // Same complete-export pattern as Chit/Inventory: one call, no page loop.
        var result = await _repository.GetPagedAsync(Common.GetCompanyCode(), filter,
            1, int.MaxValue, HttpContext.RequestAborted);
        return Success(result.Items);
    }
}
