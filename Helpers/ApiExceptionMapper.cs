using API_AMNOTE_WEB.Repositories;

namespace API_AMNOTE_WEB.Helpers
{
    public static class ApiExceptionMapper
    {
        public static ApiResponse Map(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException unauthorized => ApiResponse.Unauthorized(
                    string.IsNullOrWhiteSpace(unauthorized.Message) ? null : unauthorized.Message),
                ArgumentNullException => ApiResponse.BadRequest("Dữ liệu không được để trống"),
                ArgumentException argument => ApiResponse.ValidationError(argument.Message),
                KeyNotFoundException notFound => ApiResponse.NotFound(notFound.Message),
                InvalidOperationException invalidOperation => ApiResponse.BusinessError(invalidOperation.Message),
                _ => ApiResponse.ServerError($"Lỗi: {exception.Message}")
            };
        }
    }
}
