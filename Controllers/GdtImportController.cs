using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services.Tax;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GdtImportController : BaseApiController
    {
        private readonly IGdtImportService _service;

        public GdtImportController(IGdtImportService service)
        {
            _service = service;
        }

        /// <summary>Lưu danh sách summary trước (C99 SaveInvoiceList). Trả NeedDetail từng HĐ.</summary>
        [HttpPost("upsert-list")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public Task<IActionResult> UpsertList(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
            => ExecuteUpsert(request, listOnly: true, cancellationToken);

        /// <summary>Lưu JSON chi tiết sau khi kéo detail (batch).</summary>
        [HttpPost("upsert-json")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public Task<IActionResult> UpsertJson(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
            => ExecuteUpsert(request, listOnly: false, cancellationToken);

        /// <summary>Upsert gộp header + json (tương thích cũ).</summary>
        [HttpPost("upsert")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<IActionResult> Upsert(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
        {
            var gate = await ValidateAndAuthorizeAsync(request);
            if (gate != null)
            {
                return gate;
            }

            try
            {
                var result = await _service.UpsertAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    request!,
                    cancellationToken);
                return Success(result, $"Đã lưu {result.Upserted}/{result.Received} hóa đơn");
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        /// <summary>Danh sách tài khoản GDT đã lưu (InitSavedLogin).</summary>
        [HttpGet("logins")]
        public async Task<IActionResult> ListLogins(CancellationToken cancellationToken)
        {
            await EnsureVatListPermissionAsync();
            var data = await _service.ListSavedLoginsAsync(Common.GetCompanyCode(), cancellationToken);
            return Success(data);
        }

        /// <summary>Lưu tài khoản GDT sau đồng bộ (SaveGdtLoginInfo).</summary>
        [HttpPost("save-login")]
        public async Task<IActionResult> SaveLogin(
            [FromBody] GdtSaveLoginRequest? request,
            CancellationToken cancellationToken)
        {
            await EnsureVatListPermissionAsync();
            if (request == null)
            {
                return ValidationError("Body không hợp lệ");
            }

            try
            {
                await _service.SaveLoginAsync(
                    Common.GetCompanyCode(),
                    request.Username,
                    request.Password,
                    request.Token,
                    cancellationToken);
                return Success(true, "Đã lưu tài khoản GDT");
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        private async Task EnsureVatListPermissionAsync()
        {
            try
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "VIEW");
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "ADD");
                }
                catch (UnauthorizedAccessException)
                {
                    await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EDIT");
                }
            }
        }

        private async Task<IActionResult> ExecuteUpsert(
            GdtImportUpsertRequest? request,
            bool listOnly,
            CancellationToken cancellationToken)
        {
            var gate = await ValidateAndAuthorizeAsync(request);
            if (gate != null)
            {
                return gate;
            }

            try
            {
                var result = listOnly
                    ? await _service.UpsertListAsync(
                        Common.GetCompanyCode(),
                        Common.GetUserId(),
                        request!,
                        cancellationToken)
                    : await _service.UpsertJsonAsync(
                        Common.GetCompanyCode(),
                        Common.GetUserId(),
                        request!,
                        cancellationToken);

                var message = listOnly
                    ? $"Đã lưu list {result.ListSaved}/{result.Received}; cần detail {result.NeedDetailCount}"
                    : $"Đã lưu JSON {result.JsonSaved}/{result.Received}";
                return Success(result, message);
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        private async Task<IActionResult?> ValidateAndAuthorizeAsync(GdtImportUpsertRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Body không hợp lệ");
            }

            if (request.Items == null)
            {
                return ValidationError("Thiếu items");
            }

            if (request.Items.Count > GdtImportService.MaxItems)
            {
                return ValidationError($"Số hóa đơn vượt giới hạn {GdtImportService.MaxItems}");
            }

            try
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "ADD");
            }
            catch (UnauthorizedAccessException)
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EDIT");
            }

            return null;
        }
    }
}
