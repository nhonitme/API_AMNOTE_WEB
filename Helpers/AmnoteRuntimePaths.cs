namespace API_AMNOTE_WEB.Helpers;

/// <summary>
/// Resolves writable App_Data for jobs/temp/debug/logs.
/// On macOS/Linux the publish folder is often SMB-deployed and file-watched — writing
/// job/debug/SQL-log files there recycled the API mid-import ("API đã restart").
/// Override with env AMNOTE_APP_DATA when needed.
/// </summary>
public static class AmnoteRuntimePaths
{
    public static string GetAppDataRoot(string? contentRootPath)
    {
        var env = Environment.GetEnvironmentVariable("AMNOTE_APP_DATA");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env.Trim());
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            return Path.Combine(Path.GetTempPath(), "amnote-app-data");
        }

        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            return Path.Combine(contentRootPath, "App_Data");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "App_Data");
    }

    public static string CombineAppData(string? contentRootPath, params string[] relativeParts)
    {
        var root = GetAppDataRoot(contentRootPath);
        if (relativeParts == null || relativeParts.Length == 0)
        {
            return root;
        }

        var segments = new string[relativeParts.Length + 1];
        segments[0] = root;
        Array.Copy(relativeParts, 0, segments, 1, relativeParts.Length);
        return Path.Combine(segments);
    }
}
