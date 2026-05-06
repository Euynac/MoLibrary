namespace Monica.AI.Skills.Models;

/// <summary>
/// Runtime policy for Monica's read-only file access skill.
/// </summary>
/// <remarks>
/// These limits protect the chat loop from oversized local filesystem output. Individual tool calls may request
/// smaller limits, but cannot exceed these configured caps.
/// </remarks>
internal sealed class ReadOnlyFileAccessOptions
{
    /// <summary>
    /// Configured filesystem roots that the skill may inspect. When this list is empty, the built-in skill is disabled.
    /// </summary>
    public IReadOnlyList<ReadOnlyFileAccessRootRegistration> Roots { get; init; } = [];

    /// <summary>
    /// Ripgrep executable path. Defaults to <c>rg</c>, which resolves from the host process PATH.
    /// </summary>
    public string RipgrepExecutablePath { get; init; } = "rg";

    /// <summary>
    /// Maximum seconds a ripgrep process may run before it is cancelled.
    /// </summary>
    public int RipgrepTimeoutSeconds { get; init; } = 20;

    /// <summary>
    /// Maximum number of file-content lines returned by a single read call.
    /// </summary>
    public int MaxReadLines { get; init; } = 400;

    /// <summary>
    /// Default number of file-content lines returned when a read call does not request a specific limit.
    /// </summary>
    public int DefaultReadLines { get; init; } = 160;

    /// <summary>
    /// Maximum token budget returned by a single read call after line-window selection.
    /// </summary>
    public int MaxReadTokens { get; init; } = 12000;

    /// <summary>
    /// Default token budget returned by a read call after line-window selection.
    /// </summary>
    public int DefaultReadTokens { get; init; } = 6000;

    /// <summary>
    /// Maximum results returned by a search or file-listing call.
    /// </summary>
    public int MaxResults { get; init; } = 200;

    /// <summary>
    /// Default results returned by a search or file-listing call.
    /// </summary>
    public int DefaultResults { get; init; } = 50;

    /// <summary>
    /// Maximum context lines allowed before or after each ripgrep search match.
    /// </summary>
    public int MaxSearchContextLines { get; init; } = 5;

    /// <summary>
    /// Maximum characters returned for one search result line. Longer lines are truncated in Monica output even when
    /// ripgrep can still match them.
    /// </summary>
    public int MaxSearchLineCharacters { get; init; } = 500;
}
