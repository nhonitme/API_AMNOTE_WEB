using API_AMNOTE_WEB.Einvoice.Helpers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoicePrintImageHelper
    {
        public static string ResolveTransformImage(
            string? xslContent,
            string? dbPath,
            string xslParamName,
            IWebHostEnvironment environment,
            ILogger? logger = null)
        {
            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                var fromDb = ResolveImageParameter(dbPath, environment, logger, xslParamName);
                if (IsBrowserLoadableImage(fromDb))
                {
                    return fromDb;
                }

                logger?.LogWarning(
                    "E-invoice image {Param} from DB path is not browser-loadable. path={Path}",
                    xslParamName,
                    dbPath);
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(xslContent))
            {
                var fromXsl = ReadXslImageParam(xslContent, xslParamName);
                if (!string.IsNullOrWhiteSpace(fromXsl))
                {
                    var fromXslResolved = ResolveImageParameter(fromXsl, environment, logger, xslParamName);
                    if (IsBrowserLoadableImage(fromXslResolved))
                    {
                        return fromXslResolved;
                    }

                    logger?.LogWarning(
                        "E-invoice image {Param} from XSL param is not browser-loadable.",
                        xslParamName);
                }
            }

            return string.Empty;
        }

        public static string ResolveImageParameter(
            string? path,
            IWebHostEnvironment environment,
            ILogger? logger = null,
            string? paramName = null)
        {
            var normalized = path?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (IsBrowserLoadableImage(normalized))
            {
                return normalized;
            }

            if (EInvoiceFtpClient.TryCanonicalRemoteImagePath(normalized, out var remotePath))
            {
                normalized = remotePath;
            }

            // Windows treats "/DATA2/..." as a rooted local path. Never probe the disk for FTP folders.
            if (IsFtpRemotePath(normalized))
            {
                var cached = TryReadCachedDataUri(normalized, environment);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    return cached;
                }

                var fromFtp = TryDownloadFtpAsDataUri(normalized, environment, logger, paramName);
                if (!string.IsNullOrWhiteSpace(fromFtp))
                {
                    return fromFtp;
                }

                logger?.LogWarning(
                    "E-invoice image {Param} FTP download produced no data URI. path={Path} ftpConfigured={Configured}",
                    paramName,
                    normalized,
                    EInvoiceFtpClient.IsConfigured());
                return string.Empty;
            }

            foreach (var candidate in EnumerateCandidatePaths(normalized, environment))
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                var dataUri = TryCreateDataUri(candidate);
                if (dataUri != null)
                {
                    return dataUri;
                }
            }

            logger?.LogWarning(
                "E-invoice image {Param} path is not a local file, HTTP URL, or known FTP folder. path={Path}",
                paramName,
                normalized);
            return string.Empty;
        }

        public static void SaveCachedImage(string remotePath, byte[] bytes, IWebHostEnvironment environment)
        {
            if (bytes.Length < 32)
            {
                return;
            }

            var cachePath = GetCachePath(remotePath, environment);
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllBytes(cachePath, bytes);
        }

        public static byte[]? TryReadCachedBytes(string remotePath, IWebHostEnvironment environment)
        {
            var cachePath = GetCachePath(remotePath, environment);
            if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath) || new FileInfo(cachePath).Length < 32 || !LooksLikeImage(cachePath))
            {
                return null;
            }

            return File.ReadAllBytes(cachePath);
        }

        public static string? ReadXslImageParam(string xslContent, string paramName)
        {
            if (string.IsNullOrWhiteSpace(xslContent))
            {
                return null;
            }

            var pattern = $"""<xsl:param\s+name="{Regex.Escape(paramName)}"\s+select="(?<value>.*?)"\s*/>""";
            var match = Regex.Match(xslContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                return null;
            }

            return EvaluateXPathStringLiteral(match.Groups["value"].Value.Trim());
        }

        public static string ClearXslImageParams(string xslContent)
        {
            if (string.IsNullOrWhiteSpace(xslContent))
            {
                return xslContent;
            }

            var result = xslContent;
            result = UpsertEmptyImageParam(result, "logoImage");
            result = UpsertEmptyImageParam(result, "backgroundImage");
            result = UpsertEmptyImageParam(result, "nenImage");
            result = UpsertEmptyImageParam(result, "vienHdImage");
            return result;
        }

        private static string UpsertEmptyImageParam(string xslContent, string paramName)
        {
            var pattern = $"""(<xsl:param\s+name="{Regex.Escape(paramName)}"\s+select=")[^"]*("\s*/>)""";
            if (Regex.IsMatch(xslContent, pattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(xslContent, pattern, "$1''$2", RegexOptions.IgnoreCase);
            }

            return xslContent;
        }

        private static bool IsBrowserLoadableImage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string? EvaluateXPathStringLiteral(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var decoded = expression
                .Replace("&quot;", "\"", StringComparison.Ordinal)
                .Replace("&apos;", "'", StringComparison.Ordinal)
                .Trim();

            if (decoded.StartsWith("concat(", StringComparison.OrdinalIgnoreCase)
                && decoded.EndsWith(')'))
            {
                var parts = Regex.Matches(decoded, @"'([^']*)'")
                    .Select(m => m.Groups[1].Value)
                    .ToArray();
                return parts.Length == 0 ? null : string.Concat(parts);
            }

            if (decoded.Length >= 2
                && ((decoded[0] == '\'' && decoded[^1] == '\'')
                    || (decoded[0] == '"' && decoded[^1] == '"')))
            {
                return decoded[1..^1];
            }

            return decoded;
        }

        private static bool IsFtpRemotePath(string path)
            => EInvoiceFtpClient.IsRemotePath(path);

        private static string? TryDownloadFtpAsDataUri(
            string remotePath,
            IWebHostEnvironment environment,
            ILogger? logger = null,
            string? paramName = null)
        {
            try
            {
                if (!EInvoiceFtpClient.IsConfigured())
                {
                    logger?.LogWarning("E-invoice image FTP is not configured. {Param} path={Path}", paramName, remotePath);
                    return null;
                }

                var cachePath = GetCachePath(remotePath, environment);
                if (string.IsNullOrWhiteSpace(cachePath))
                {
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                var cacheOk = File.Exists(cachePath)
                    && new FileInfo(cachePath).Length > 32
                    && LooksLikeImage(cachePath);

                if (!cacheOk)
                {
                    EInvoiceFtpClient.DownloadFile(remotePath, cachePath);
                    if (!File.Exists(cachePath) || new FileInfo(cachePath).Length < 32 || !LooksLikeImage(cachePath))
                    {
                        logger?.LogWarning(
                            "FTP image download empty or invalid. {Param} path={Path} exists={Exists} length={Length}",
                            paramName,
                            remotePath,
                            File.Exists(cachePath),
                            File.Exists(cachePath) ? new FileInfo(cachePath).Length : 0);
                        return null;
                    }
                }

                return TryCreateDataUri(cachePath);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "FTP image download failed. {Param} path={Path}", paramName, remotePath);
                return null;
            }
        }

        private static bool LooksLikeImage(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                Span<byte> header = stackalloc byte[12];
                var read = stream.Read(header);
                if (read < 4)
                {
                    return false;
                }

                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                {
                    return true;
                }

                if (header[0] == 0xFF && header[1] == 0xD8)
                {
                    return true;
                }

                if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
                {
                    return true;
                }

                if (header[0] == 0x42 && header[1] == 0x4D)
                {
                    return true;
                }

                return read >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            }
            catch
            {
                return false;
            }
        }

        private static string? TryReadCachedDataUri(string remotePath, IWebHostEnvironment environment)
        {
            var cachePath = GetCachePath(remotePath, environment);
            if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath) || new FileInfo(cachePath).Length < 32 || !LooksLikeImage(cachePath))
            {
                return null;
            }

            return TryCreateDataUri(cachePath);
        }

        private static string GetCachePath(string remotePath, IWebHostEnvironment environment)
        {
            var cacheDir = Path.Combine(environment.ContentRootPath, "App_Data", "einvoice-image-cache");
            var cacheFileName = Regex.Replace(Path.GetFileName(remotePath.Replace('\\', '/')), @"[^\w\.\-]", "_");
            if (string.IsNullOrWhiteSpace(cacheFileName))
            {
                return string.Empty;
            }

            return Path.Combine(cacheDir, cacheFileName);
        }

        private static IEnumerable<string> EnumerateCandidatePaths(string path, IWebHostEnvironment environment)
        {
            if (IsFtpRemotePath(path))
            {
                yield break;
            }

            if (Path.IsPathRooted(path) && !path.StartsWith("/DATA2/", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
                yield break;
            }

            var trimmed = path.TrimStart('/', '\\');
            yield return Path.Combine(environment.ContentRootPath, path);
            yield return Path.Combine(environment.ContentRootPath, trimmed);
            yield return Path.Combine(environment.WebRootPath, trimmed);
        }

        private static string? TryCreateDataUri(string filePath)
        {
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var mime = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "application/octet-stream",
                };

                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return null;
            }
        }
    }
}
