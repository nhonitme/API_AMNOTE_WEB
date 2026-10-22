using Microsoft.Extensions.Options;
using API_AMNOTE_WEB.Repositories;

namespace API_AMNOTE_WEB.Middleware;

public enum MobileDeviceMode { Observe, Enforce }

public sealed class MobileDeviceOptions
{
    public bool Enabled { get; set; } = true;
    public MobileDeviceMode Mode { get; set; } = MobileDeviceMode.Enforce;
}

public static class PhoneDeviceDetector
{
    // Viewport and touch support describe layout, not the device type.
    public static bool IsPhone(string userAgent, string mobileHint)
    {
        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("iphone") || ua.Contains("ipod") || ua.Contains("windows phone")) return true;
        if (ua.Contains("ipad") || ua.Contains("tablet") || ua.Contains("kindle") || ua.Contains("silk/")) return false;
        if (ua.Contains("android")) return ua.Contains("mobile") || mobileHint.Trim() == "?1";
        return mobileHint.Trim() == "?1";
    }
}

public sealed class MobileDeviceMiddleware
{
    public const string ErrorCode = "MOBILE_DEVICE_BLOCKED";
    public const string Message = "AMNote không hỗ trợ sử dụng trên điện thoại. Vui lòng truy cập bằng máy tính.";
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<MobileDeviceOptions> _options;
    private readonly ILogger<MobileDeviceMiddleware> _logger;
    private long _nextLogTicks;

    public MobileDeviceMiddleware(RequestDelegate next, IOptionsMonitor<MobileDeviceOptions> options,
        ILogger<MobileDeviceMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public static bool IsProtectedRequest(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method)) return false;
        var path = (request.Path.Value ?? "").TrimEnd('/');
        // Exact methods and paths: do not exempt future public endpoints implicitly.
        if (HttpMethods.IsGet(request.Method) &&
            (path.Equals("/api/public/einvoice/lookup", StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/api/public/einvoice/minutes/lookup", StringComparison.OrdinalIgnoreCase))) return false;
        if (HttpMethods.IsPost(request.Method) &&
            (path.Equals("/api/public/einvoice/minutes/lookup/signature", StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))) return false;
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
               request.Path.StartsWithSegments("/DXXRDV", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled || !IsProtectedRequest(context.Request) ||
            !PhoneDeviceDetector.IsPhone(context.Request.Headers.UserAgent.ToString(),
                context.Request.Headers["Sec-CH-UA-Mobile"].ToString()))
        {
            await _next(context);
            return;
        }

        // Bound log volume without retaining client identifiers or raw headers.
        var now = DateTime.UtcNow.Ticks;
        var previous = Interlocked.Read(ref _nextLogTicks);
        if (now >= previous && Interlocked.CompareExchange(ref _nextLogTicks,
                now + TimeSpan.FromSeconds(30).Ticks, previous) == previous)
            _logger.LogInformation("Phone device policy {Mode}. Method: {Method}, Path: {Path}, Trace: {Trace}",
                options.Mode, context.Request.Method, context.Request.Path, context.TraceIdentifier);

        if (options.Mode == MobileDeviceMode.Observe)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers.CacheControl = "no-store";
        var response = ApiResponse.Forbidden(Message);
        response.Data = new { Code = ErrorCode };
        await context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
}
