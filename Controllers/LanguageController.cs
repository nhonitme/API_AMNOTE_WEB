using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    public class LanguageController : BaseApiController
    {
        private readonly ILanguageRepository _languageRepository;

        public LanguageController(ILanguageRepository languageRepository)
        {
            _languageRepository = languageRepository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] string key = "")
        {
            var languageItems = await _languageRepository.getLanguageInfoAll(string.IsNullOrWhiteSpace(key) ? Array.Empty<string>() : new[] { key.Trim() });
            return Success(languageItems);
        }
    }
}