using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/license")]
    public sealed class LicenseInfoController : BaseApiController
    {
        private readonly LicenseInfoRepository _repository;

        public LicenseInfoController(LicenseInfoRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// GET /api/license/current - read-only license for the authenticated company.
        /// Returns Data = null when no license record has been provisioned.
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var companyCd = Common.GetCompanyCode();
            return Success(await _repository.GetCurrentAsync(companyCd));
        }
    }
}
