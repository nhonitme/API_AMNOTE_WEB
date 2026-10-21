using API_AMNOTE_WEB.Constants;
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
        private readonly ILogger<LicenseInfoController> _logger;

        public LicenseInfoController(
            LicenseInfoRepository repository,
            ILogger<LicenseInfoController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/license/current - read-only license for the authenticated company.
        /// Returns Data = null with a localized Message if no license has been provisioned.
        /// Status codes remain language-independent; the frontend translates their labels.
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var companyCd = Common.GetCompanyCode();
            try
            {
                var license = await _repository.GetCurrentAsync(companyCd);
                if (license == null)
                {
                    var message = await ResolveMessageAsync(
                        LicenseLanguageKeys.NotConfigured, "Chưa có thông tin License");
                    return Success(null, message);
                }

                return Success(license);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load license information for company {CompanyCd}", companyCd);
                var message = await ResolveMessageAsync(
                    LicenseLanguageKeys.LoadFailed, "Không thể tải thông tin License.");
                return ServerError(message);
            }
        }

        private static async Task<string> ResolveMessageAsync(string key, string fallback)
        {
            try
            {
                var message = await Common.getLanguage(key, Common.GetCurrentLanguage());
                return string.IsNullOrWhiteSpace(message) ||
                    string.Equals(message, key, StringComparison.OrdinalIgnoreCase)
                    ? fallback
                    : message;
            }
            catch
            {
                // Translation lookup must not replace the original License API result.
                return fallback;
            }
        }
    }
}
