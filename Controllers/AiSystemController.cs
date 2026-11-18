using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class AiSystemController : BaseApiController
    {
        private readonly IAiSystemService _aiSystemService;

        public AiSystemController(IAiSystemService aiSystemService)
        {
            _aiSystemService = aiSystemService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var companyCd = Common.GetCompanyCode();
            var status = await _aiSystemService.GetStatusAsync(companyCd);
            return Success(status);
        }

        [HttpPost("init")]
        public async Task<IActionResult> Init(CancellationToken cancellationToken)
        {
            var companyCd = Common.GetCompanyCode();
            var result = await _aiSystemService.InitAsync(companyCd, cancellationToken);
            if (!result.Success)
            {
                return BusinessError(result.Message ?? "Khởi tạo AI thất bại.", result);
            }

            return Success(result, result.Message);
        }

        [HttpPost("suggest-accounts")]
        public async Task<IActionResult> SuggestAccounts(
            [FromBody] AiSuggestAccountsRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return ValidationError("Request body is required");
            }

            try
            {
                var companyCd = Common.GetCompanyCode();
                var result = await _aiSystemService.SuggestAccountsAsync(companyCd, request, cancellationToken);
                return Success(result);
            }
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
        }

        [HttpPost("translate")]
        public async Task<IActionResult> Translate(
            [FromBody] AiTranslateRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return ValidationError("Request body is required");
            }

            try
            {
                var companyCd = Common.GetCompanyCode();
                var result = await _aiSystemService.TranslateAsync(companyCd, request, cancellationToken);
                return Success(result);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
        }
    }
}
