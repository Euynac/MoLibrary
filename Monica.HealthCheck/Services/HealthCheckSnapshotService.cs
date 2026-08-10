using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.HealthCheck.Models;

namespace Monica.HealthCheck.Services;

internal sealed class HealthCheckSnapshotService(HealthCheckService healthCheckService)
{
    private const int MAX_DATA_ENTRIES = 32;
    private const int MAX_TEXT_LENGTH = 2 * 1024;

    private static readonly string[] sensitiveKeyFragments =
    [
        "password",
        "secret",
        "token",
        "credential",
        "connectionstring"
    ];

    public async Task<HealthCheckSnapshot> GetSnapshotAsync(
        HealthCheckScope scope,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(
            registration => IsInScope(registration, scope),
            cancellationToken);

        return new HealthCheckSnapshot
        {
            Scope = scope,
            Status = MapStatus(report.Status),
            CheckedAt = DateTimeOffset.UtcNow,
            Duration = report.TotalDuration,
            Entries = report.Entries
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => CreateEntry(pair.Key, pair.Value))
                .ToArray()
        };
    }

    private static bool IsInScope(HealthCheckRegistration registration, HealthCheckScope scope)
    {
        return scope switch
        {
            HealthCheckScope.All => true,
            HealthCheckScope.Readiness => registration.Tags.Contains(
                HealthCheckTags.Ready,
                StringComparer.OrdinalIgnoreCase),
            HealthCheckScope.Liveness => registration.Tags.Contains(
                HealthCheckTags.Live,
                StringComparer.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown health-check scope.")
        };
    }

    private static HealthCheckEntrySnapshot CreateEntry(string name, HealthReportEntry entry)
    {
        return new HealthCheckEntrySnapshot
        {
            Name = name,
            Status = MapStatus(entry.Status),
            Description = Bound(entry.Description),
            Duration = entry.Duration,
            Tags = entry.Tags.Order(StringComparer.Ordinal).ToArray(),
            Data = SanitizeData(entry.Data),
            Error = entry.Exception is null
                ? null
                : new HealthCheckErrorSnapshot
                {
                    Type = entry.Exception.GetType().FullName ?? entry.Exception.GetType().Name,
                    Message = Bound(entry.Exception.Message) ?? string.Empty
                }
        };
    }

    private static IReadOnlyDictionary<string, string> SanitizeData(
        IReadOnlyDictionary<string, object> data)
    {
        var sanitized = data
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Take(MAX_DATA_ENTRIES)
            .ToDictionary(
                static pair => pair.Key,
                pair => IsSensitive(pair.Key)
                    ? "[redacted]"
                    : FormatValue(pair.Value),
                StringComparer.Ordinal);

        return new ReadOnlyDictionary<string, string>(sanitized);
    }

    private static bool IsSensitive(string key)
    {
        return sensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatValue(object value)
    {
        try
        {
            return Bound(Convert.ToString(value, CultureInfo.InvariantCulture)) ?? string.Empty;
        }
        catch (Exception ex)
        {
            // A diagnostic object's formatting failure must not suppress otherwise valid health results.
            return $"[unavailable: {ex.GetType().Name}]";
        }
    }

    private static string? Bound(string? value)
    {
        if (value is null || value.Length <= MAX_TEXT_LENGTH)
        {
            return value;
        }

        return value[..MAX_TEXT_LENGTH];
    }

    private static HealthCheckState MapStatus(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => HealthCheckState.Healthy,
            HealthStatus.Degraded => HealthCheckState.Degraded,
            HealthStatus.Unhealthy => HealthCheckState.Unhealthy,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown health-check status.")
        };
    }
}
