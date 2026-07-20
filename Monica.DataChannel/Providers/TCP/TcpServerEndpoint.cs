using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;
using Monica.DataChannel.Providers.TCP.Utils;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.TCP;

/// <summary>
/// Connects a data-channel pipeline to a TCP listener owned by the current host.
/// </summary>
/// <param name="metadata">The TCP listener configuration.</param>
/// <param name="logger">The host logger for connection events.</param>
/// <param name="manager">The current host's data-channel manager.</param>
/// <param name="runtime">The current host's TCP connection runtime.</param>
public class TcpServerEndpoint(
    TcpServerOptions metadata,
    ILogger<TcpServerEndpoint> logger,
    IDataChannelManager manager,
    TcpConnectionRuntime runtime) : CommunicationEndpointBase<TcpServerOptions>(metadata)
{
    private TcpServerExtends? _server;

    /// <inheritdoc />
    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        var key = data.Metadata.GetOrDefault("ConnectionName") as string;
        var message = data.Data?.ToString();

        if (!key.IsNullOrEmptySet())
        {
            if (runtime.TryGetClient(key, out var client) && client is not null)
            {
                await client.SendMsg(message, logger, manager);
            }

            return;
        }

        foreach (var client in runtime.GetClients())
        {
            await client.SendMsg(message, logger, manager);
        }
    }

    /// <inheritdoc />
    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        _server = new TcpServerExtends(runtime);
        _server.ReceivedMsgEvent += eventArgs =>
        {
            var data = CreateData(eventArgs.Data);
            data.Metadata.Set("ConnectionName", eventArgs.ConnectionName);
            SendData(data);
        };
        _server.Init(metadata, logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }

    /// <inheritdoc />
    public override Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        _server?.Dispose();
        return base.DisposeAsync(cancellationToken);
    }
}
