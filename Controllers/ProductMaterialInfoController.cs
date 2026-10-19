using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductMaterialInfoController : BaseApiController
    {
        // Reuse the already configured master-inventory permissions.
        private const string MenuCode = "MD_INVENTORY";
        private readonly ProductMaterialInfoRepository _repository;

        public ProductMaterialInfoController(ProductMaterialInfoRepository repository)
            => _repository = repository;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] long productId)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            if (productId <= 0) return ValidationError("PRODUCT_ID phải lớn hơn 0.");
            return Success(await _repository.GetListAsync(Common.GetCompanyCode(), productId));
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            return Success(await _repository.GetSummaryAsync(Common.GetCompanyCode()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductMaterialSaveRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");
            var invalid = Validate(request);
            if (invalid != null) return ValidationError(invalid);
            try
            {
                var id = await _repository.SaveAsync(Common.GetCompanyCode(), Common.GetUserId(), 0, request!);
                return Created(new { MATERIAL_ID = id });
            }
            catch (MySqlException ex) when (ex.SqlState == "45000")
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpPut("{materialId:long}")]
        public async Task<IActionResult> Update(long materialId, [FromBody] ProductMaterialSaveRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");
            if (materialId <= 0) return ValidationError("MATERIAL_ID phải lớn hơn 0.");
            var invalid = Validate(request);
            if (invalid != null) return ValidationError(invalid);
            try
            {
                var id = await _repository.SaveAsync(Common.GetCompanyCode(), Common.GetUserId(), materialId, request!);
                return Updated(new { MATERIAL_ID = id });
            }
            catch (MySqlException ex) when (ex.SqlState == "45000")
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpDelete("{materialId:long}")]
        public async Task<IActionResult> Delete(long materialId)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");
            if (materialId <= 0) return ValidationError("MATERIAL_ID phải lớn hơn 0.");
            try
            {
                await _repository.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), materialId);
                return Success(new { MATERIAL_ID = materialId });
            }
            catch (MySqlException ex) when (ex.SqlState == "45000")
            {
                return ValidationError(ex.Message);
            }
        }

        private static string? Validate(ProductMaterialSaveRequest? request)
        {
            if (request == null) return "Thiếu nội dung yêu cầu.";
            if (request.PRODUCT_ID <= 0) return "PRODUCT_ID phải lớn hơn 0.";
            if (request.MATERIAL_PRODUCT_ID <= 0) return "MATERIAL_PRODUCT_ID phải lớn hơn 0.";
            if (request.PRODUCT_ID == request.MATERIAL_PRODUCT_ID)
                return "Thành phẩm không thể là nguyên vật liệu của chính nó.";
            if (request.QUANTITY <= 0 || request.QUANTITY > 999999999999.999999m)
                return "QUANTITY phải lớn hơn 0 và không vượt giới hạn DECIMAL(18,6).";
            return null;
        }
    }
}
