using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class AiPostingRuleController : BaseApiController
    {
        private readonly IAiPostingRuleService _service;

        public AiPostingRuleController(IAiPostingRuleService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            try
            {
                var companyCd = Common.GetCompanyCode();
                var items = await _service.GetListAsync(companyCd);
                return Success(new { data = items });
            }
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
        }

        [HttpGet("{ruleId:long}")]
        public async Task<IActionResult> GetDetail(long ruleId)
        {
            try
            {
                var companyCd = Common.GetCompanyCode();
                var item = await _service.GetDetailAsync(companyCd, ruleId);
                if (item == null)
                {
                    return NotFound("Không tìm thấy quy tắc AI.");
                }

                return Success(item);
            }
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] AiPostingRuleDetailDto request)
        {
            if (request == null)
            {
                return ValidationError("Request body is required");
            }

            try
            {
                var companyCd = Common.GetCompanyCode();
                var userId = Common.GetUserId();
                var ruleId = await _service.SaveAsync(companyCd, userId, request);
                var detail = await _service.GetDetailAsync(companyCd, ruleId);
                return Success(detail, "Đã lưu quy tắc AI.");
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        [HttpDelete("{ruleId:long}")]
        public async Task<IActionResult> Delete(long ruleId)
        {
            try
            {
                var companyCd = Common.GetCompanyCode();
                var userId = Common.GetUserId();
                await _service.DeleteAsync(companyCd, userId, ruleId);
                return Deleted(message: "Đã xóa quy tắc AI.");
            }
            catch (InvalidOperationException ex)
            {
                return Forbidden(ex.Message);
            }
        }
    }
}
