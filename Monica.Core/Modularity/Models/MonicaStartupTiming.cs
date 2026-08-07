namespace Monica.Core.Modularity.Models;

/// <summary>
/// Describes an explicitly tracked application startup interval ending when the Generic Host publishes
/// <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime.ApplicationStarted"/>.
/// </summary>
/// <remarks>
/// The duration uses a monotonic clock and begins at the application-owned <see cref="MonicaStartup"/> marker.
/// Work performed before the marker and unbounded background warm-up after <c>ApplicationStarted</c> are excluded.
/// </remarks>
public sealed record MonicaStartupTiming
{
    /// <summary>Gets the UTC timestamp paired with the application-owned monotonic startup origin.</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the Generic Host published <c>ApplicationStarted</c>, or
    /// <see langword="null"/> while the application is not yet ready.
    /// </summary>
    public DateTimeOffset? ReadyAtUtc { get; init; }

    /// <summary>
    /// Gets the monotonic duration from the application startup marker until <c>ApplicationStarted</c>, in
    /// milliseconds, or <see langword="null"/> before the application becomes ready.
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>Gets whether the Generic Host has published <c>ApplicationStarted</c>.</summary>
    public bool IsReady => ReadyAtUtc.HasValue && DurationMs.HasValue;
}
