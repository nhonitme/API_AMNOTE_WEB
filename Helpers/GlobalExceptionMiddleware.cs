using API_AMNOTE_WEB.Helpers;
using System.Text.Json;

namespace API_AMNOTE_WEB.Middleware
{
    public sealed class GlobalExceptionMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;


        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                _logger.LogError(
                    ex,
                    "Request failed: {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                var response = ApiExceptionMapper.Map(ex);
                context.Response.Clear();
                context.Response.StatusCode = response.StatusCode.GetHttpStatusCode();
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(response, JsonOptions);
            }
        }
    }
}
