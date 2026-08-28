using Monica.SignalR.Models;

namespace Monica.SignalR.Metrics;

/// <summary>
/// Describes a SignalR client target selected by <see cref="Microsoft.AspNetCore.SignalR.IHubClients{T}"/>.
/// </summary>
internal sealed class SignalRSendTarget
{
    /// <summary>
    /// Gets the target kind.
    /// </summary>
    public SignalRSendTargetKind Kind { get; init; }

    /// <summary>
    /// Gets the target identifiers when they are available and capture is enabled.
    /// </summary>
    public IReadOnlyList<string> Identifiers { get; init; } = [];

    /// <summary>
    /// Gets the human-friendly display names aligned by index with <see cref="Identifiers"/>.
    /// </summary>
    /// <remarks>
    /// Entries are empty when no display name could be resolved, for example when a user target has no live
    /// connection. Display names never take part in metric identity, so a target row keeps its identity even when
    /// the resolved names change between sends.
    /// </remarks>
    public IReadOnlyList<string> IdentifierDisplayNames { get; init; } = [];

    /// <summary>
    /// Gets the number of explicit target or exclusion identifiers supplied for this target selection.
    /// </summary>
    /// <remarks>
    /// This is not a recipient count or connected client count. <see cref="SignalRSendTargetKind.All"/> has no
    /// explicit target identifiers, so the value is zero.
    /// </remarks>
    public int TargetCount { get; init; }

    /// <summary>
    /// Creates an unqualified target descriptor.
    /// </summary>
    public static SignalRSendTarget Create(SignalRSendTargetKind kind)
    {
        return new SignalRSendTarget
        {
            Kind = kind,
            TargetCount = kind == SignalRSendTargetKind.All ? 0 : 1
        };
    }

    /// <summary>
    /// Creates a target descriptor for one identifier.
    /// </summary>
    public static SignalRSendTarget Create(SignalRSendTargetKind kind, string identifier, bool includeIdentifier)
    {
        return Create(kind, [identifier], [], includeIdentifier);
    }

    /// <summary>
    /// Creates a target descriptor for multiple identifiers.
    /// </summary>
    public static SignalRSendTarget Create(
        SignalRSendTargetKind kind,
        IReadOnlyList<string> identifiers,
        bool includeIdentifiers)
    {
        return Create(kind, identifiers, [], includeIdentifiers);
    }

    /// <summary>
    /// Creates a target descriptor for multiple identifiers with optional display names.
    /// </summary>
    /// <param name="kind">The target kind.</param>
    /// <param name="identifiers">The explicit target identifiers supplied for this selection.</param>
    /// <param name="identifierDisplayNames">
    /// Display names aligned by index with <paramref name="identifiers"/>; missing or shorter entries fall back to
    /// an empty display name.</param>
    /// <param name="includeIdentifiers">Whether identifier capture is enabled for diagnostics.</param>
    public static SignalRSendTarget Create(
        SignalRSendTargetKind kind,
        IReadOnlyList<string> identifiers,
        IReadOnlyList<string>? identifierDisplayNames,
        bool includeIdentifiers)
    {
        var capturedIdentifiers = includeIdentifiers ? identifiers.ToList() : [];
        var capturedDisplayNames = includeIdentifiers && capturedIdentifiers.Count > 0
            ? AlignDisplayNames(capturedIdentifiers, identifierDisplayNames)
            : [];

        return new SignalRSendTarget
        {
            Kind = kind,
            Identifiers = capturedIdentifiers,
            IdentifierDisplayNames = capturedDisplayNames,
            TargetCount = identifiers.Count
        };
    }

    private static List<string> AlignDisplayNames(
        IReadOnlyList<string> identifiers,
        IReadOnlyList<string>? identifierDisplayNames)
    {
        var displayNames = new List<string>(identifiers.Count);
        for (var index = 0; index < identifiers.Count; index++)
        {
            displayNames.Add(index < identifierDisplayNames?.Count ? identifierDisplayNames[index] : string.Empty);
        }

        return displayNames;
    }
}
