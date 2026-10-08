using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class BaseApiController : ControllerBase
    {
        #region Success Response Helpers

        protected IActionResult Success(object data = null, string message = null)
        {
            var response = ApiResponse.Ok(data, message);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Success(object data, string message, ApiStatusCode statusCode)
        {
            var response = ApiResponse.Ok(data, message, statusCode);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Created(object data = null, string message = null)
        {
            var response = ApiResponse.Created(data, message);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Updated(object data = null, string message = null)
        {
            var response = ApiResponse.Updated(data, message);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Deleted(object data = null, string message = null)
        {
            var response = ApiResponse.Deleted(data, message);
            return StatusCode(response.Status, response);
        }

        #endregion Success Response Helpers

        #region Error Response Helpers

        protected IActionResult Error(ApiStatusCode statusCode, string message = null, object data = null)
        {
            var response = ApiResponse.Error(statusCode, message, data);
            return StatusCode(response.Status, response);
        }

        protected IActionResult BadRequest(string message, object errors = null)
        {
            var response = ApiResponse.BadRequest(message, errors);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Unauthorized(string message = null)
        {
            var response = ApiResponse.Unauthorized(message);
            return StatusCode(response.Status, response);
        }

        protected IActionResult Forbidden(string message = null)
        {
            var response = ApiResponse.Forbidden(message);
            return StatusCode(response.Status, response);
        }

        protected IActionResult NotFound(string message, object data = null)
        {
            var response = ApiResponse.NotFound(message, data);
            return StatusCode(response.Status, response);
        }

        protected IActionResult ValidationError(string message, object errors = null)
        {
            var response = ApiResponse.ValidationError(message, errors);
            return StatusCode(response.Status, response);
        }

        protected IActionResult ServerError(string message = null, object data = null)
        {
            var response = ApiResponse.ServerError(message, data);
            return StatusCode(response.Status, response);
        }

        protected IActionResult BusinessError(string message, object data = null)
        {
            var response = ApiResponse.BusinessError(message, data);
            return StatusCode(response.Status, response);
        }

        protected async Task<IActionResult?> CheckMasterInUseAsync(IMasterInUseRepository repository, string companyCd, string masterType, long masterId, string? masterCd)
        {
            var result = await repository.CheckInUseAsync(companyCd, masterType, masterId, masterCd);
            if (!result.IsUsed)
            {
                return null;
            }

            var message = string.IsNullOrWhiteSpace(result.MESSAGE) ? "Danh mục đã được sử dụng." : result.MESSAGE;
            return BusinessError(message, result);
        }

        #endregion Error Response Helpers

        #region Paged Response Helper

        protected IActionResult PagedSuccess<T>(
            IEnumerable<T> data,
            int pageNumber,
            int pageSize,
            int totalRecords,
            string message = null)
        {
            var response = PagedApiResponse<T>.Ok(data, pageNumber, pageSize, totalRecords, message);
            return StatusCode(response.Status, response);
        }

        #endregion Paged Response Helper

        #region Preview/Export Result Helper

        protected async Task<IActionResult> FromPreviewAsync(
            Func<Task<IActionResult>> action,
            string? notFoundMessage = null,
            bool includeArgumentException = false)
        {
            try
            {
                return await action();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(notFoundMessage ?? ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (ArgumentException ex) when (includeArgumentException)
            {
                return ValidationError(ex.Message);
            }
        }

        #endregion Preview/Export Result Helper

        protected async Task EnsurePermissionAsync(string menuCode, string permissionKey)
        {
            var hasPermission = await Common.HasPermissionAsync(menuCode, permissionKey);
            if (!hasPermission)
            {
                throw new UnauthorizedAccessException("Access denied");
            }
        }

        protected static string ResolveLang(string? lang)
            => string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();

        protected static async Task<string> BuildMessageAsync(string fieldKey, string messageKey, string lang)
            => await Common.BuildCombinedMessageAsync(fieldKey, messageKey, lang);

        protected static Task<string> FieldRequiredAsync(string fieldKey, string? lang = null)
            => Common.GetFieldRequiredMessageAsync(fieldKey, lang ?? ResolveLang(null));

        #region check json

        protected object GetModelStateErrors()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new
                {
                    Field = x.Key,
                    Errors = x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? e.Exception?.Message
                        : e.ErrorMessage).ToList()
                })
                .ToList();

            return new
            {
                Message = "Model binding failed",
                Errors = errors
            };
        }

        #endregion check json
    }
}
