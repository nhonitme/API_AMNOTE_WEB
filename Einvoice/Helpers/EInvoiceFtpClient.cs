using API_AMNOTE_WEB.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    internal static class EInvoiceFtpClient
    {
        public const string DefaultXmlRemoteDirectory = "/home/WEB/amnoteweb/EInvoiceXMLv2";
        public const string DefaultImageRemoteDirectory = "/home/WEB/amnoteweb/EinvoiceImage";
        public const string DefaultNenRemoteDirectory = "/home/WEB/amnoteweb/EinvoiceNen";
        public const string DefaultVienRemoteDirectory = "/home/WEB/amnoteweb/EinvoiceVien";
        public const string DefaultXslRemoteDirectory = "/home/WEB/amnoteweb/EinvoiceXSL";
        public const string DefaultPitXslRemoteDirectory = "/home/WEB/amnoteweb/PitXSL";
        public const string DefaultCompanyXslRemoteDirectory = "/home/WEB/amnoteweb/EinvoiceXSLCompany";
        public const string DefaultCompanySignaturesRemoteDirectory = "/home/WEB/amnoteweb/CompanySignatures";
        public const string DefaultUserAvatarsRemoteDirectory = "/home/WEB/amnoteweb/UserAvatars";
        private const string LegacyAttachRoot = "/DATA2/AttachFile";
        private const string CurrentAttachRoot = "/home/WEB/amnoteweb";

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"
        };

        private static readonly HashSet<string> XslExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xsl", ".xslt"
        };

        private static readonly Regex CompanyUploadFileName = new(
            @"^[A-Za-z0-9][\w\-]*_(?:\d+_d\d+|pit_\d+)_(?:logo|background|invoice-background|border|nen)_[0-9a-f]{8,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static FtpEndpoint _endpoint = FtpEndpoint.Empty;
        private static string _defaultXslRemoteDirectory = DefaultXslRemoteDirectory;
        private static string _pitXslRemoteDirectory = DefaultPitXslRemoteDirectory;

        public static void Configure(IConfiguration configuration)
        {
            var ftp = configuration.GetSection("FtpImage");
            _defaultXslRemoteDirectory = NormalizeRemoteDirectory(
                ftp["DefaultXslRemoteDirectory"]?.Trim() is { Length: > 0 } configuredXsl
                    ? configuredXsl
                    : DefaultXslRemoteDirectory);
            _pitXslRemoteDirectory = NormalizeRemoteDirectory(
                ftp["PitXslRemoteDirectory"]?.Trim() is { Length: > 0 } configuredPitXsl
                    ? configuredPitXsl
                    : DefaultPitXslRemoteDirectory);
            _endpoint = new FtpEndpoint(
                Host: ftp["Host"]?.Trim() ?? string.Empty,
                FtpPort: ftp.GetValue("Port", 21),
                SftpPort: ftp.GetValue("SftpPort", 22),
                Username: ftp["Username"]?.Trim() ?? string.Empty,
                Password: ftp["Password"] ?? string.Empty,
                Protocol: RemoteFileTransfer.ParseProtocol(ftp["Protocol"], "FTP"));
        }

        public static bool TryNormalizeImageKind(string? imageKind, out string normalizedKind)
        {
            normalizedKind = (imageKind ?? string.Empty).Trim().ToLowerInvariant();
            return normalizedKind is "logo" or "background" or "invoice-background" or "border";
        }

        public static string ResolveImageRemoteDirectory(string imageKind)
        {
            return imageKind.Trim().ToLowerInvariant() switch
            {
                "invoice-background" or "nen" => DefaultNenRemoteDirectory,
                "border" or "invoice-border" or "vien" => DefaultVienRemoteDirectory,
                _ => DefaultImageRemoteDirectory,
            };
        }

        public static bool TrySanitizeCompanySegment(string? companyCd, out string company)
        {
            company = Regex.Replace((companyCd ?? string.Empty).Trim(), @"[^\w\-]", "_");
            return !string.IsNullOrWhiteSpace(company) && company is not "." and not "..";
        }

        public static string ResolveCompanyImageRemoteDirectory(string imageKind, string companyCd)
        {
            if (!TrySanitizeCompanySegment(companyCd, out var company))
            {
                throw new ArgumentException("Company code is required");
            }

            return $"{NormalizeRemoteDirectory(ResolveImageRemoteDirectory(imageKind))}/{company}";
        }

        public static bool TryBuildLibraryImagePath(string imageKind, string? fileName, out string remotePath)
        {
            remotePath = string.Empty;
            if (!TryNormalizeImageFileName(fileName, out var name))
            {
                return false;
            }

            remotePath = $"{NormalizeRemoteDirectory(ResolveImageRemoteDirectory(imageKind))}/{name}";
            return true;
        }

        public static bool TryBuildCompanyImagePath(string imageKind, string companyCd, string? fileName, out string remotePath)
        {
            remotePath = string.Empty;
            if (!TrySanitizeCompanySegment(companyCd, out _) || !TryNormalizeImageFileName(fileName, out var name))
            {
                return false;
            }

            remotePath = $"{ResolveCompanyImageRemoteDirectory(imageKind, companyCd)}/{name}";
            return true;
        }

        public static bool TryCanonicalRemoteImagePath(string? path, out string remotePath)
        {
            remotePath = string.Empty;
            var stored = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(stored)
                || stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || stored.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsRemotePath(stored))
            {
                remotePath = NormalizeRemotePath(stored);
                return ImageExtensions.Contains(Path.GetExtension(stored));
            }

            var withSlash = stored.StartsWith('/') ? stored : "/" + stored;
            if (IsRemotePath(withSlash))
            {
                remotePath = NormalizeRemotePath(withSlash);
                return ImageExtensions.Contains(Path.GetExtension(withSlash));
            }

            var name = Path.GetFileName(stored);
            if (string.IsNullOrWhiteSpace(name)
                || name is "." or ".."
                || name.Contains("..", StringComparison.Ordinal)
                || !ImageExtensions.Contains(Path.GetExtension(name)))
            {
                return false;
            }

            if (stored.Contains('/') && !stored.Equals("/" + name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryBuildLibraryImagePath(InferImageKindFromFileName(name), name, out remotePath);
        }

        public static bool TryResolveImageRemotePath(string imageKind, string? fileName, string? storedPath, out string remotePath)
        {
            if (TryCanonicalRemoteImagePath(storedPath, out remotePath))
            {
                return true;
            }

            return TryBuildLibraryImagePath(imageKind, fileName, out remotePath);
        }

        public static bool TryResolveAccessibleImagePath(
            string imageKind,
            string companyCd,
            string? fileName,
            string? storedPath,
            out string remotePath)
        {
            remotePath = string.Empty;
            if (TryCanonicalRemoteImagePath(storedPath, out var candidate)
                && CanCompanyAccessImagePath(imageKind, companyCd, candidate))
            {
                remotePath = candidate;
                return true;
            }

            if (TryBuildLibraryImagePath(imageKind, fileName, out candidate)
                && CanCompanyAccessImagePath(imageKind, companyCd, candidate))
            {
                remotePath = candidate;
                return true;
            }

            return TryBuildCompanyImagePath(imageKind, companyCd, fileName, out remotePath)
                && CanCompanyAccessImagePath(imageKind, companyCd, remotePath);
        }

        public static bool CanCompanyAccessImagePath(string imageKind, string companyCd, string remotePath)
            => IsSharedLibraryImagePath(imageKind, remotePath)
            || IsCompanyOwnedImagePath(imageKind, companyCd, remotePath);

        public static bool IsSharedLibraryImagePath(string imageKind, string remotePath)
        {
            if (!TryCanonicalRemoteImagePath(remotePath, out var path))
            {
                return false;
            }

            var libraryDir = NormalizeRemoteDirectory(ResolveImageRemoteDirectory(imageKind));
            return IsDirectChildOf(path, libraryDir) && !LooksLikeCompanyUploadFileName(Path.GetFileName(path));
        }

        public static bool IsCompanyOwnedImagePath(string imageKind, string companyCd, string remotePath)
        {
            if (!TrySanitizeCompanySegment(companyCd, out var company)
                || !TryCanonicalRemoteImagePath(remotePath, out var path))
            {
                return false;
            }

            var libraryDir = NormalizeRemoteDirectory(ResolveImageRemoteDirectory(imageKind));
            var companyDir = $"{libraryDir}/{company}";
            if (IsDirectChildOf(path, companyDir))
            {
                return true;
            }

            if (!IsDirectChildOf(path, libraryDir))
            {
                return false;
            }

            var name = Path.GetFileName(path);
            return name.StartsWith(company + "_", StringComparison.OrdinalIgnoreCase)
                && LooksLikeCompanyUploadFileName(name);
        }

        public static string? ResolveDraftImagePath(string imageKind, string companyCd, params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (TryResolveAccessibleImagePath(imageKind, companyCd, candidate, candidate, out var remotePath))
                {
                    return remotePath;
                }
            }

            return null;
        }

        public static IReadOnlyList<EInvoiceFtpImageFile> ListImageFiles(string imageKind, string? companyCd = null)
        {
            var libraryDir = NormalizeRemoteDirectory(ResolveImageRemoteDirectory(imageKind));
            var files = new Dictionary<string, EInvoiceFtpImageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in RemoteFileTransfer.ListNames(ToTransfer(libraryDir), libraryDir))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(name)))
                {
                    continue;
                }

                var path = $"{libraryDir}/{name}";
                if (IsSharedLibraryImagePath(imageKind, path))
                {
                    files[path] = new EInvoiceFtpImageFile
                    {
                        FILE_NAME = name,
                        PATH = path,
                        SOURCE = "library",
                    };
                }
                else if (!string.IsNullOrWhiteSpace(companyCd) && IsCompanyOwnedImagePath(imageKind, companyCd, path))
                {
                    files[path] = new EInvoiceFtpImageFile
                    {
                        FILE_NAME = name,
                        PATH = path,
                        SOURCE = "company",
                    };
                }
            }

            if (TrySanitizeCompanySegment(companyCd, out _))
            {
                var companyDir = ResolveCompanyImageRemoteDirectory(imageKind, companyCd!);
                foreach (var name in RemoteFileTransfer.ListNames(ToTransfer(companyDir), companyDir))
                {
                    if (!ImageExtensions.Contains(Path.GetExtension(name)))
                    {
                        continue;
                    }

                    var path = $"{companyDir}/{name}";
                    files[path] = new EInvoiceFtpImageFile
                    {
                        FILE_NAME = name,
                        PATH = path,
                        SOURCE = "company",
                    };
                }
            }

            return files.Values
                .OrderBy(item => string.Equals(item.SOURCE, "company", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(item => item.FILE_NAME, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ResolveDefaultXslRemoteDirectory()
            => NormalizeRemoteDirectory(_defaultXslRemoteDirectory);

        public static string ResolvePitXslRemoteDirectory()
            => NormalizeRemoteDirectory(_pitXslRemoteDirectory);

        public static bool TryBuildPitXslPath(string? fileName, out string remotePath)
            => TryBuildXslPath(ResolvePitXslRemoteDirectory(), fileName, out remotePath);

        public static bool TryBuildDefaultXslPath(string? fileName, out string remotePath)
            => TryBuildXslPath(ResolveDefaultXslRemoteDirectory(), fileName, out remotePath);

        private static bool TryBuildXslPath(string directory, string? fileName, out string remotePath)
        {
            remotePath = string.Empty;
            var name = Path.GetFileName((fileName ?? string.Empty).Trim().Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            if (!XslExtensions.Contains(Path.GetExtension(name)))
            {
                return false;
            }

            remotePath = $"{directory}/{name}";
            return true;
        }

        public static IReadOnlyList<EInvoiceFtpXslFile> ListDefaultXslFiles()
        {
            var directory = ResolveDefaultXslRemoteDirectory();
            var files = new List<EInvoiceFtpXslFile>();
            foreach (var name in RemoteFileTransfer.ListNames(ToTransfer(directory), directory))
            {
                if (!XslExtensions.Contains(Path.GetExtension(name)))
                {
                    continue;
                }

                files.Add(new EInvoiceFtpXslFile
                {
                    FILE_NAME = name,
                    PATH = $"{directory}/{name}",
                });
            }

            return files
                .OrderBy(item => item.FILE_NAME, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool RemoteFileExists(string remotePath)
            => RemoteFileTransfer.Exists(ToTransfer(remotePath), remotePath);

        public static string GetImageContentType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/png",
            };
        }

        public static bool IsConfigured()
            => _endpoint.IsConfigured;

        public static bool IsRemotePath(string? path)
        {
            var normalized = path?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remote = normalized.Replace('\\', '/');
            return remote.StartsWith(CurrentAttachRoot, StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(LegacyAttachRoot, StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultXmlRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EInvoiceXMLv2/", StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceXml/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultImageRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceImage/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultNenRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceNen/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultVienRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceVien/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultXslRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceXSL/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultPitXslRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/PitXSL/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultCompanyXslRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/EinvoiceXSLCompany/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultCompanySignaturesRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/CompanySignatures/", StringComparison.OrdinalIgnoreCase)
                || remote.StartsWith(DefaultUserAvatarsRemoteDirectory, StringComparison.OrdinalIgnoreCase)
                || remote.Contains("/UserAvatars/", StringComparison.OrdinalIgnoreCase);
        }

        public static void UploadText(string remotePath, string content, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            UploadBytes(remotePath, encoding.GetBytes(content ?? string.Empty));
        }

        public static string DownloadText(string remotePath, Encoding? encoding = null)
        {
            var bytes = DownloadBytes(remotePath);
            return encoding == null ? DecodeText(bytes) : encoding.GetString(bytes);
        }

        /// <summary>
        /// Detect UTF-8 / UTF-16 BOM so Windows-saved XSL is not decoded as garbage.
        /// </summary>
        public static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            // UTF-16 LE without BOM: many nulls in odd positions.
            if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0 && bytes[0] != 0)
            {
                return Encoding.Unicode.GetString(bytes);
            }

            return Encoding.UTF8.GetString(bytes);
        }

        public static bool LooksLikeXmlOrXsl(string? content, out string? reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                reason = "XSL content is empty";
                return false;
            }

            var trimmed = content.TrimStart('\uFEFF', '\u200B', ' ', '\t', '\r', '\n');
            if (trimmed.Length == 0)
            {
                reason = "XSL content is empty";
                return false;
            }

            if (trimmed[0] != '<')
            {
                var preview = trimmed.Length > 80 ? trimmed[..80] : trimmed;
                reason = $"XSL content is not XML (does not start with '<'). Preview: {preview}";
                return false;
            }

            if (!trimmed.Contains("xsl:", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Contains("stylesheet", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Contains("transform", StringComparison.OrdinalIgnoreCase))
            {
                reason = "Downloaded file does not look like an XSL stylesheet";
                return false;
            }

            return true;
        }

        public static void UploadBytes(string remotePath, byte[] content)
            => RemoteFileTransfer.UploadBytes(ToTransfer(remotePath), remotePath, content);

        public static byte[] DownloadBytes(string remotePath)
            => RemoteFileTransfer.DownloadBytes(ToTransfer(remotePath), remotePath);

        public static void DownloadFile(string remotePath, string localFilePath)
            => RemoteFileTransfer.DownloadFile(ToTransfer(remotePath), remotePath, localFilePath);

        public static void EnsureRemoteDirectoryExists(string remoteDirectory)
            => RemoteFileTransfer.EnsureDirectory(ToTransfer(remoteDirectory), remoteDirectory);

        private static RemoteFileTransfer.Endpoint ToTransfer(string remotePath)
        {
            return new RemoteFileTransfer.Endpoint(
                _endpoint.Host,
                _endpoint.FtpPort,
                _endpoint.SftpPort,
                _endpoint.Username,
                _endpoint.Password,
                _endpoint.Protocol);
        }

        private static string NormalizeRemotePath(string remotePath)
        {
            var normalized = remotePath.Trim().Replace('\\', '/');
            if (!normalized.StartsWith('/'))
            {
                normalized = "/" + normalized;
            }

            if (normalized.Equals(LegacyAttachRoot, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(LegacyAttachRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                var rest = normalized[LegacyAttachRoot.Length..];
                normalized = CurrentAttachRoot + (rest.StartsWith('/') ? rest : "/" + rest);
            }

            if (!normalized.StartsWith(CurrentAttachRoot, StringComparison.OrdinalIgnoreCase))
            {
                string[] shortFolders =
                [
                    "/EinvoiceImage", "/EinvoiceNen", "/EinvoiceVien",
                    "/EInvoiceXMLv2", "/EinvoiceXSL", "/EinvoiceXSLCompany", "/EinvoiceXml",
                    "/CompanySignatures", "/UserAvatars",
                ];
                foreach (var folder in shortFolders)
                {
                    if (normalized.Equals(folder, StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        normalized = CurrentAttachRoot + normalized;
                        break;
                    }
                }
            }

            return normalized;
        }

        private static string NormalizeRemoteDirectory(string remoteDirectory)
            => NormalizeRemotePath(remoteDirectory).TrimEnd('/');

        private static bool TryNormalizeImageFileName(string? fileName, out string name)
        {
            name = Path.GetFileName((fileName ?? string.Empty).Trim().Replace('\\', '/'));
            return !string.IsNullOrWhiteSpace(name)
                && name is not "." and not ".."
                && !name.Contains("..", StringComparison.Ordinal)
                && ImageExtensions.Contains(Path.GetExtension(name));
        }

        private static bool LooksLikeCompanyUploadFileName(string? fileName)
            => !string.IsNullOrWhiteSpace(fileName) && CompanyUploadFileName.IsMatch(Path.GetFileName(fileName));

        private static bool IsDirectChildOf(string remotePath, string directory)
        {
            var path = NormalizeRemotePath(remotePath);
            var dir = NormalizeRemoteDirectory(directory);
            if (!path.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = path[(dir.Length + 1)..];
            return rest.Length > 0 && !rest.Contains('/');
        }

        private static string InferImageKindFromFileName(string fileName)
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("_nen_") || lower.Contains("-nen-"))
            {
                return "invoice-background";
            }

            if (lower.Contains("_vien_") || lower.Contains("-vien-"))
            {
                return "border";
            }

            return lower.Contains("_background_") || lower.Contains("-background-")
                ? "background"
                : "logo";
        }

        private readonly record struct FtpEndpoint(
            string Host,
            int FtpPort,
            int SftpPort,
            string Username,
            string Password,
            RemoteFileTransfer.TransferProtocol Protocol)
        {
            public static FtpEndpoint Empty => new(
                string.Empty,
                21,
                22,
                string.Empty,
                string.Empty,
                RemoteFileTransfer.TransferProtocol.Ftp);

            public bool IsConfigured =>
                !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username);
        }
    }
}
