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
    /// Gets the number of explicit target identifiers supplied for this target selection.
    /// </summary>
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
        return new SignalRSendTarget
        {
            Kind = kind,
            Identifiers = includeIdentifier ? [identifier] : [],
            TargetCount = 1
        };
    }

    /// <summary>
    /// Creates a target descriptor for multiple identifiers.
    /// </summary>
    public static SignalRSendTarget Create(
        SignalRSendTargetKind kind,
        IReadOnlyList<string> identifiers,
        bool includeIdentifiers)
    {
        return new SignalRSendTarget
        {
            Kind = kind,
            Identifiers = includeIdentifiers ? identifiers.ToList() : [],
            TargetCount = identifiers.Count
        };
    }
}
