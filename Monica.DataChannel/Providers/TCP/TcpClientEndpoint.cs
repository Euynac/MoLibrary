using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;
using Monica.DataChannel.Providers.TCP.Utils;

namespace Monica.DataChannel.Providers.TCP;

/// <summary>
/// Connects a data-channel pipeline to an outbound TCP client owned by the current host.
/// </summary>
/// <param name="metadata">The TCP client configuration.</param>
/// <param name="logger">The host logger for connection events.</param>
/// <param name="manager">The current host's data-channel manager.</param>
/// <param name="runtime">The current host's TCP connection runtime.</param>
public class TcpClientEndpoint(
    TcpClientOptions metadata,
    ILogger<TcpClientEndpoint> logger,
    IDataChannelManager manager,
    TcpConnectionRuntime runtime) : CommunicationEndpointBase<TcpClientOptions>(metadata)
{
    private TcpClientExtends? _client;

    /// <inheritdoc />
    public override Task ReceiveDataAsync(ChannelDataContext data) =>
        GetClient().SendMsg(data.Data?.ToString(), logger, manager);

    /// <inheritdoc />
    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        _client = new TcpClientExtends(runtime);
        _client.MsgReceivedEvent += eventArgs => SendData(eventArgs);
        _client.Init(metadata, logger);
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
        _client?.Dispose();
        return base.DisposeAsync(cancellationToken);
    }

    private TcpClientExtends GetClient()
    {
        return _client ?? throw new InvalidOperationException("TCP client is not initialized.");
    }
}
