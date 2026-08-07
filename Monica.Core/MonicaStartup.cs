using System.Diagnostics;

namespace Monica.Core;

/// <summary>
/// Marks an application-owned startup origin that Monica can observe until the Generic Host publishes
/// <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.ApplicationStarted"/>.
/// </summary>
/// <remarks>
/// Create the marker as early as practical in the application's entry point, then pass it to the matching
/// <c>AddMonica</c> overload. A marker is intentionally single-use so parallel hosts cannot share timing state.
/// </remarks>
public sealed class MonicaStartup
{
    private int _isClaimed;

    private MonicaStartup()
    {
        StartedAtUtc = DateTimeOffset.UtcNow;
        StartedTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>Gets the UTC timestamp paired with this marker's monotonic origin.</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>Creates a new single-use application startup marker.</summary>
    /// <returns>A marker that can be supplied to one <c>AddMonica</c> invocation.</returns>
    public static MonicaStartup Start() => new();

    internal long StartedTimestamp { get; }

    internal MonicaStartupOrigin Claim()
    {
        if (Interlocked.Exchange(ref _isClaimed, 1) != 0)
        {
            throw new InvalidOperationException(
                "This Monica startup marker has already been assigned to a host.");
        }

        return new MonicaStartupOrigin(StartedAtUtc, StartedTimestamp);
    }
}

/// <summary>Transfers an application startup marker into one host-owned Monica application.</summary>
internal readonly record struct MonicaStartupOrigin(
    DateTimeOffset StartedAtUtc,
    long StartedTimestamp);
