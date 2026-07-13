namespace Monica.Configuration.Models;

/// <summary>
/// Describes a failed side effect that occurred after configuration data was durably committed.
/// </summary>
public sealed record ConfigurationPostCommitIssue
{
    /// <summary>
    /// Gets the issue category used by callers to select a user-facing message.
    /// </summary>
    public ConfigurationPostCommitIssueKind Kind { get; init; }

    /// <summary>
    /// Gets the component or notifier that reported the issue.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets a concise English diagnostic suitable for API consumers and logs.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets complete recursive exception detail for diagnostics.
    /// </summary>
    public required string Detail { get; init; }
}
