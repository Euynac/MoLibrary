using Microsoft.AspNetCore.SignalR;
using Monica.SignalR.Models;

namespace Monica.SignalR.Metrics;

/// <summary>
/// Wraps strongly typed SignalR hub clients and instruments returned client proxies.
/// </summary>
internal sealed class SignalRSendMetricsHubClients<TContract>(
    IHubClients<TContract> inner,
    SignalRSendMetrics metrics,
    string hubName,
    bool includeTargetIdentifiers)
    : IHubClients<TContract>
    where TContract : class
{
    /// <inheritdoc />
    public TContract All => Track(inner.All, SignalRSendTarget.Create(SignalRSendTargetKind.All));

    /// <inheritdoc />
    public TContract AllExcept(IReadOnlyList<string> excludedConnectionIds)
    {
        return Track(
            inner.AllExcept(excludedConnectionIds),
            SignalRSendTarget.Create(SignalRSendTargetKind.AllExcept, excludedConnectionIds, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract Client(string connectionId)
    {
        return Track(
            inner.Client(connectionId),
            SignalRSendTarget.Create(SignalRSendTargetKind.Client, connectionId, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract Clients(IReadOnlyList<string> connectionIds)
    {
        return Track(
            inner.Clients(connectionIds),
            SignalRSendTarget.Create(SignalRSendTargetKind.Clients, connectionIds, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract Group(string groupName)
    {
        return Track(
            inner.Group(groupName),
            SignalRSendTarget.Create(SignalRSendTargetKind.Group, groupName, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract Groups(IReadOnlyList<string> groupNames)
    {
        return Track(
            inner.Groups(groupNames),
            SignalRSendTarget.Create(SignalRSendTargetKind.Groups, groupNames, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
    {
        var identifiers = new[] { groupName }
            .Concat(excludedConnectionIds)
            .ToList();

        return Track(
            inner.GroupExcept(groupName, excludedConnectionIds),
            SignalRSendTarget.Create(SignalRSendTargetKind.GroupExcept, identifiers, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract User(string userId)
    {
        return Track(
            inner.User(userId),
            SignalRSendTarget.Create(SignalRSendTargetKind.User, userId, includeTargetIdentifiers));
    }

    /// <inheritdoc />
    public TContract Users(IReadOnlyList<string> userIds)
    {
        return Track(
            inner.Users(userIds),
            SignalRSendTarget.Create(SignalRSendTargetKind.Users, userIds, includeTargetIdentifiers));
    }

    private TContract Track(TContract clientProxy, SignalRSendTarget target)
    {
        return metrics.IsEnabled
            ? SignalRSendMetricsDispatch<TContract>.Create(clientProxy, metrics, hubName, target)
            : clientProxy;
    }
}
