using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Middleware
{
    public class CompanyRouteContextMiddleware
    {
        public const string CompanyCdItemKey = "CompanyCd";
        public const string CompanyCdHeaderName = "X-Company-CD";
        private static readonly PathString CompanyRoutePrefix = new("/api/companies");
        private static readonly Regex CompanyCodePattern = new("^[A-Za-z0-9_-]{1,20}$", RegexOptions.Compiled);
        private readonly RequestDelegate _next;
        private readonly ILogger<CompanyRouteContextMiddleware> _logger;

        public CompanyRouteContextMiddleware(RequestDelegate next, ILogger<CompanyRouteContextMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (TryResolveRouteCompanyCd(context, out var routeCompanyCd, out var unscopedPath, out var routeError))
            {
                if (!string.IsNullOrWhiteSpace(routeError))
                {
                    _logger.LogWarning("Company context route error. Method: {Method}, Path: {Path}, Query: {Query}, Error: {Error}",
                        context.Request.Method,
                        context.Request.Path.Value,
                        context.Request.QueryString.Value,
                        routeError);
                    await WriteCompanyContextErrorAsync(context, routeError);
                    return;
                }

                if (!TryResolveHeaderCompanyCd(context, out var headerCompanyCd))
                {
                    await WriteCompanyContextErrorAsync(context, "Company code is invalid");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(headerCompanyCd) &&
                    !string.Equals(headerCompanyCd, routeCompanyCd, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Company code mismatch. Route: {RouteCompanyCd}, Header: {HeaderCompanyCd}", routeCompanyCd, headerCompanyCd);
                    await WriteCompanyContextErrorAsync(context, "Company context is inconsistent");
                    return;
                }

                SetCompanyCode(context, routeCompanyCd);
                context.Request.Path = unscopedPath;
                await _next(context);
                return;
            }

            if (!TryResolveHeaderCompanyCd(context, out var companyCd))
            {
                await WriteCompanyContextErrorAsync(context, "Company code is invalid");
                return;
            }

            if (!string.IsNullOrWhiteSpace(companyCd))
            {
                SetCompanyCode(context, companyCd);
            }
            else if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
                     !context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) &&
                     !context.Request.Path.StartsWithSegments("/api/public", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Company context was not supplied. Method: {Method}, Path: {Path}, Query: {Query}, HeaderCompanyCd: {HeaderCompanyCd}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Request.QueryString.Value,
                    context.Request.Headers[CompanyCdHeaderName].ToString());
            }

            await _next(context);
        }

        public static bool IsValidCompanyCode(string? companyCd)
        {
            return !string.IsNullOrWhiteSpace(companyCd) && CompanyCodePattern.IsMatch(companyCd.Trim());
        }

        public static bool TryGetCompanyCode(HttpContext? context, out string companyCd)
        {
            companyCd = (context?.Items[CompanyCdItemKey] as string ?? string.Empty).Trim();
            return IsValidCompanyCode(companyCd);
        }

        public static string GetRequiredCompanyCode(HttpContext? context)
        {
            if (!TryGetCompanyCode(context, out var companyCd))
            {
                throw new UnauthorizedAccessException("Company code not found in request context");
            }

            return companyCd;
        }

        private static void SetCompanyCode(HttpContext context, string companyCd)
        {
            context.Items[CompanyCdItemKey] = companyCd.Trim();
        }

        private bool TryResolveRouteCompanyCd(
            HttpContext context,
            out string companyCd,
            out PathString unscopedPath,
            out string? error)
        {
            companyCd = string.Empty;
            unscopedPath = context.Request.Path;
            error = null;

            if (!context.Request.Path.StartsWithSegments(CompanyRoutePrefix, StringComparison.OrdinalIgnoreCase, out var remainingPath))
            {
                return false;
            }

            var remaining = remainingPath.Value ?? string.Empty;
            if (!remaining.StartsWith("/", StringComparison.Ordinal) || remaining.Length <= 1)
            {
                error = "Company route is invalid";
                return true;
            }

            var routeParts = remaining[1..].Split('/', 2);
            if (routeParts.Length != 2 || string.IsNullOrWhiteSpace(routeParts[0]) || string.IsNullOrWhiteSpace(routeParts[1]))
            {
                error = "Company route is invalid";
                return true;
            }

            companyCd = Uri.UnescapeDataString(routeParts[0]).Trim();
            if (!IsValidCompanyCode(companyCd))
            {
                _logger.LogWarning("Invalid company code in route: {CompanyCd}", companyCd);
                error = "Company code is invalid";
                return true;
            }

            unscopedPath = new PathString("/api/" + routeParts[1]);
            return true;
        }

        private static Task WriteCompanyContextErrorAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return context.Response.WriteAsJsonAsync(new { success = false, message });
        }

        private bool TryResolveHeaderCompanyCd(HttpContext context, out string companyCd)
        {
            companyCd = context.Request.Headers[CompanyCdHeaderName].ToString().Trim();

            if (string.IsNullOrWhiteSpace(companyCd))
            {
                return true;
            }

            if (IsValidCompanyCode(companyCd))
            {
                return true;
            }

            _logger.LogWarning("Invalid company code in header: {CompanyCd}", companyCd);
            companyCd = string.Empty;
            return false;
        }
    }
}
