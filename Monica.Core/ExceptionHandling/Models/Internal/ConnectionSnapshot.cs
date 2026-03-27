using Microsoft.AspNetCore.Http;

namespace Monica.Core.ExceptionHandling.Models.Internal;

internal sealed class ConnectionSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string? RemoteIpAddress { get; set; }

    public int RemotePort { get; set; }

    public string? LocalIpAddress { get; set; }

    public int LocalPort { get; set; }

    public static ConnectionSnapshot? Create(ConnectionInfo? connection)
    {
        if (connection == null)
        {
            return null;
        }

        return new ConnectionSnapshot
        {
            Id = connection.Id,
            RemoteIpAddress = connection.RemoteIpAddress?.ToString(),
            RemotePort = connection.RemotePort,
            LocalIpAddress = connection.LocalIpAddress?.ToString(),
            LocalPort = connection.LocalPort
        };
    }
}
