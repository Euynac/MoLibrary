namespace Monica.SignalR.Models;

/// <summary>
/// Describes the SignalR client target selected for an observed server-to-client send.
/// </summary>
public enum SignalRSendTargetKind
{
    /// <summary>
    /// The target could not be classified.
    /// </summary>
    Unknown,

    /// <summary>
    /// The send targets every connected client for the hub.
    /// </summary>
    All,

    /// <summary>
    /// The send targets every connected client except specific connection identifiers.
    /// </summary>
    AllExcept,

    /// <summary>
    /// The send targets a single connection identifier.
    /// </summary>
    Client,

    /// <summary>
    /// The send targets multiple connection identifiers.
    /// </summary>
    Clients,

    /// <summary>
    /// The send targets a single SignalR group.
    /// </summary>
    Group,

    /// <summary>
    /// The send targets multiple SignalR groups.
    /// </summary>
    Groups,

    /// <summary>
    /// The send targets a SignalR group except specific connection identifiers.
    /// </summary>
    GroupExcept,

    /// <summary>
    /// The send targets all connections associated with a single user identifier.
    /// </summary>
    User,

    /// <summary>
    /// The send targets all connections associated with multiple user identifiers.
    /// </summary>
    Users
}
