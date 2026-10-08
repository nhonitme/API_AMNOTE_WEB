using System.Net;

namespace API_AMNOTE_WEB.Services
{
    /// <summary>
    /// Client IP from the HTTP request only (browser header / proxy / connection).
    /// Never calls external IP APIs — those return the server's IP or hang.
    /// Browser may send <see cref="ClientIpHeader"/> after resolving via ipinfo.
    /// </summary>
    public static class ClientPublicIpResolver
    {
        public const string ClientIpHeader = "X-Client-IP";

        public static string ResolveFromRequest(HttpContext? httpContext)
        {
            if (httpContext == null)
            {
                return string.Empty;
            }

            var request = httpContext.Request;

            // Browser-resolved public IP (web fetches ipinfo once and caches).
            var clientIp = FirstIp(request.Headers[ClientIpHeader].ToString());
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                return clientIp;
            }

            // Reverse proxy / gateway (nginx, Cloudflare, sslip, etc.)
            var forwarded = FirstIp(request.Headers["X-Forwarded-For"].ToString());
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded;
            }

            var realIp = FirstIp(request.Headers["X-Real-IP"].ToString());
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }

            var remote = httpContext.Connection.RemoteIpAddress;
            if (remote == null)
            {
                return string.Empty;
            }

            if (remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            return remote.ToString();
        }

        private static string FirstIp(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // X-Forwarded-For: client, proxy1, proxy2
            var first = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(first))
            {
                return string.Empty;
            }

            return IPAddress.TryParse(first, out _) ? first : string.Empty;
        }
    }
}
