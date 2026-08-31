using System.Diagnostics;

namespace Monica.Guide.Setup;

/// <summary>
/// Reads the local Git credential for github.com so the update feed and asset downloads
/// work against private repositories without a separate product configuration. The
/// credential never leaves the machine; it is attached only to github.com requests.
/// </summary>
public static class GithubCredential
{
    private static readonly TimeSpan FillTimeout = TimeSpan.FromSeconds(8);
    private static string? _cached;

    /// <summary>The bearer token stored by the local Git credential helper, or null when absent.</summary>
    public static string? ReadToken()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "credential fill")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            process.StandardInput.Write("protocol=https\nhost=github.com\n\n");
            process.StandardInput.Flush();
            string? password = null;
            while (process.StandardOutput.ReadLine() is { } line)
            {
                if (line.StartsWith("password=", StringComparison.Ordinal))
                {
                    password = line["password=".Length..];
                    break;
                }
            }

            if (!process.WaitForExit((int)FillTimeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                _cached = password;
            }

            return password;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or System.ComponentModel.Win32Exception
                                              or IOException)
        {
            return null;
        }
    }
}
