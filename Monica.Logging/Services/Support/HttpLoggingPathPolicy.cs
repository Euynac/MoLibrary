using Microsoft.AspNetCore.Http;

namespace Monica.Logging.Services.Support;

internal static class HttpLoggingPathPolicy
{
    public static bool ShouldSkipResponseLogging(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.StartsWith("/logging-ui/files/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/logging-ui/current/export", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFileDownloadResponse(HttpContext context)
    {
        var contentDisposition = context.Response.Headers.ContentDisposition.ToString();
        return !string.IsNullOrEmpty(contentDisposition)
               && contentDisposition.Contains("attachment", StringComparison.OrdinalIgnoreCase);
    }
}
