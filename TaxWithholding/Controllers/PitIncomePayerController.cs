using API_AMNOTE_WEB.Controllers;
using API_AMNOTE_WEB.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.TaxWithholding;

[ApiController, Authorize, Route("api/pit-withholding/income-payer")]
public sealed class PitIncomePayerController(PitIncomePayerService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get()
        => Success(await service.GetAsync(Common.GetCompanyCode()));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] PitIncomePayerSaveRequest request)
        => Success(await service.SaveAsync(
            Common.GetCompanyCode(),
            Common.GetUserId(),
            request));
}
