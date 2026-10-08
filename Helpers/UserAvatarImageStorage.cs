using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Einvoice.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace API_AMNOTE_WEB.Helpers
{
    public static class UserAvatarImageStorage
    {
        public const string LegacyPublicRoot = "/uploads/user-avatars";
        public const string RemoteRoot = EInvoiceFtpClient.DefaultUserAvatarsRemoteDirectory;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp",
            ".bmp"
        };

        public static async Task<string> SaveAsync(
            IWebHostEnvironment environment,
            string companyCd,
            long userPkId,
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            _ = environment;

            if (file.Length <= 0)
            {
                throw new InvalidOperationException("Empty image file");
            }

            if (file.Length > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("Image exceeds 2MB limit");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Unsupported image type");
            }

            var safeCompanyCd = SanitizeSegment(companyCd);
            if (string.IsNullOrWhiteSpace(safeCompanyCd))
            {
                throw new InvalidOperationException("Company code is required");
            }

            if (userPkId <= 0)
            {
                throw new InvalidOperationException("User id is required");
            }

            if (!EInvoiceFtpClient.IsConfigured())
            {
                throw new InvalidOperationException("FTP is not configured (FtpImage)");
            }

            await using var input = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();

            var fileName = $"{userPkId}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var remotePath = $"{RemoteRoot}/{safeCompanyCd}/{fileName}".Replace('\\', '/');

            await Task.Run(() => EInvoiceFtpClient.UploadBytes(remotePath, bytes), cancellationToken);
            TryWriteCache(environment, remotePath, bytes);

            return remotePath;
        }

        public static bool TryDeleteStoredPath(IWebHostEnvironment environment, string? storedPath)
        {
            var normalized = Common.NormalizeNullableText(storedPath);
            if (normalized == null)
            {
                return false;
            }

            if (IsLegacyLocalPath(normalized)
                && TryResolvePhysicalPath(environment, normalized, out var physicalPath))
            {
                try
                {
                    if (File.Exists(physicalPath))
                    {
                        File.Delete(physicalPath);
                        return true;
                    }
                }
                catch
                {
                    // Best-effort cleanup only.
                }

                return false;
            }

            TryDeleteCache(environment, normalized);
            return false;
        }

        public static bool IsLegacyLocalPath(string? path)
        {
            var normalized = Common.NormalizeNullableText(path);
            if (normalized == null)
            {
                return false;
            }

            return normalized.StartsWith(LegacyPublicRoot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("uploads/user-avatars", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFtpRemotePath(string? path)
        {
            var normalized = Common.NormalizeNullableText(path)?.Replace('\\', '/');
            if (normalized == null)
            {
                return false;
            }

            return normalized.StartsWith(RemoteRoot, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/UserAvatars/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAllowedStoredPath(string? path)
            => IsLegacyLocalPath(path) || IsFtpRemotePath(path);

        public static bool TryGetImageBytes(
            IWebHostEnvironment environment,
            string? storedPath,
            out byte[] bytes,
            out string contentType)
        {
            bytes = Array.Empty<byte>();
            contentType = "application/octet-stream";
            var normalized = Common.NormalizeNullableText(storedPath);
            if (normalized == null)
            {
                return false;
            }

            contentType = EInvoiceFtpClient.GetImageContentType(normalized);

            if (TryReadCache(environment, normalized, out bytes))
            {
                return true;
            }

            if (IsLegacyLocalPath(normalized)
                && TryResolvePhysicalPath(environment, normalized, out var physicalPath))
            {
                try
                {
                    bytes = File.ReadAllBytes(physicalPath);
                    TryWriteCache(environment, normalized, bytes);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (!IsFtpRemotePath(normalized) || !EInvoiceFtpClient.IsConfigured())
            {
                return false;
            }

            try
            {
                bytes = EInvoiceFtpClient.DownloadBytes(normalized);
                if (bytes.Length == 0)
                {
                    return false;
                }

                TryWriteCache(environment, normalized, bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolvePhysicalPath(
            IWebHostEnvironment environment,
            string? publicOrLocalPath,
            out string physicalPath)
        {
            physicalPath = string.Empty;
            var normalized = Common.NormalizeNullableText(publicOrLocalPath);
            if (normalized == null)
            {
                return false;
            }

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                physicalPath = normalized;
                return true;
            }

            if (!IsLegacyLocalPath(normalized))
            {
                return false;
            }

            var relative = normalized
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            foreach (var root in EnumerateWebRoots(environment))
            {
                var candidate = Path.GetFullPath(Path.Combine(root, relative));
                var rootFull = Path.GetFullPath(root);
                if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    physicalPath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveCacheDirectory(IWebHostEnvironment environment)
        {
            var contentRoot = string.IsNullOrWhiteSpace(environment.ContentRootPath)
                ? Directory.GetCurrentDirectory()
                : environment.ContentRootPath;
            var folder = Path.Combine(contentRoot, "App_Data", "user-avatar-cache");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string ResolveCacheFilePath(IWebHostEnvironment environment, string storedPath)
        {
            var extension = Path.GetExtension(storedPath);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                extension = ".bin";
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(storedPath.Trim().ToLowerInvariant())));
            return Path.Combine(ResolveCacheDirectory(environment), $"{hash}{extension.ToLowerInvariant()}");
        }

        private static bool TryReadCache(IWebHostEnvironment environment, string storedPath, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            try
            {
                var cachePath = ResolveCacheFilePath(environment, storedPath);
                if (!File.Exists(cachePath))
                {
                    return false;
                }

                bytes = File.ReadAllBytes(cachePath);
                return bytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void TryWriteCache(IWebHostEnvironment? environment, string storedPath, byte[] bytes)
        {
            if (environment == null || bytes.Length == 0)
            {
                return;
            }

            try
            {
                var cachePath = ResolveCacheFilePath(environment, storedPath);
                File.WriteAllBytes(cachePath, bytes);
            }
            catch
            {
                // Best-effort.
            }
        }

        private static void TryDeleteCache(IWebHostEnvironment environment, string storedPath)
        {
            try
            {
                var cachePath = ResolveCacheFilePath(environment, storedPath);
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }
            }
            catch
            {
                // Best-effort.
            }
        }

        private static IEnumerable<string> EnumerateWebRoots(IWebHostEnvironment environment)
        {
            if (!string.IsNullOrWhiteSpace(environment.WebRootPath))
            {
                yield return environment.WebRootPath;
            }

            if (!string.IsNullOrWhiteSpace(environment.ContentRootPath))
            {
                yield return Path.Combine(environment.ContentRootPath, "wwwroot");
            }

            yield return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            yield return Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        private static string SanitizeSegment(string? value)
        {
            var normalized = Common.NormalizeNullableText(value) ?? string.Empty;
            var chars = normalized
                .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
                .ToArray();
            return new string(chars);
        }
    }
}
