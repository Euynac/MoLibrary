using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Monica.Profiling.MemoryDiagnostics.Providers.DotNetTools;

internal sealed class DotNetGcDumpProvider(ILogger<DotNetGcDumpProvider> logger)
{
    public async Task<string> CollectAsync(string? outputPath = null)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var fileName = $"gcdump_{process.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.gcdump";
            var dumpPath = outputPath ?? Path.Combine(Path.GetTempPath(), fileName);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet-gcdump",
                Arguments = $"collect -p {process.Id} -o \"{dumpPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null)
            {
                throw new InvalidOperationException(
                    "Unable to start the dotnet-gcdump process. Install it with: dotnet tool install -g dotnet-gcdump");
            }

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var procError = await proc.StandardError.ReadToEndAsync();
                logger.LogWarning("dotnet-gcdump failed: {Error}", procError);
                throw new InvalidOperationException($"GC dump creation failed: {procError}");
            }

            logger.LogInformation("GC dump created at {Path}", dumpPath);
            return dumpPath;
        }
        catch (Exception ex) when (ex is Win32Exception)
        {
            throw new InvalidOperationException(
                "dotnet-gcdump is not installed. Install it with: dotnet tool install -g dotnet-gcdump",
                ex);
        }
    }
}
