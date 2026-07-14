using Microsoft.AspNetCore.Builder;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Services;

/// <summary>
/// Owns pipeline definitions and materialized channels for one application host.
/// </summary>
internal sealed class DataChannelRuntime : IDataChannelRegistrar, IDataChannelManager
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ChannelPipelineBuilder> _builders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DataChannel> _channels = new(StringComparer.Ordinal);
    private bool _isRegistrationClosed;

    /// <inheritdoc />
    public void Add(string id, Action<ChannelPipelineBuilder> configure, string? groupId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ChannelPipelineBuilder();
        configure(builder);
        builder.Prepare(id, groupId);

        lock (_syncRoot)
        {
            if (_isRegistrationClosed)
            {
                throw new InvalidOperationException(
                    "Data-channel registration is closed because the current host has already materialized its pipelines.");
            }

            if (!_builders.TryAdd(id, builder))
            {
                throw new InvalidOperationException($"Data channel '{id}' is already registered in the current host.");
            }
        }
    }

    /// <summary>
    /// Materializes every registered pipeline and applies its application-builder configuration.
    /// </summary>
    /// <param name="app">The current host's application builder.</param>
    internal void Materialize(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ChannelPipelineBuilder[] builders;
        lock (_syncRoot)
        {
            if (_isRegistrationClosed)
            {
                throw new InvalidOperationException("Data-channel pipelines have already been materialized for this host.");
            }

            _isRegistrationClosed = true;
            builders = _builders.Values.ToArray();
        }

        foreach (var builder in builders)
        {
            var pipeline = builder.Build(app.ApplicationServices);
            var channel = new DataChannel(pipeline);

            lock (_syncRoot)
            {
                _channels.Add(channel.Id, channel);
            }

            foreach (var component in pipeline.GetComponents())
            {
                if (component is IApplicationBuilderConfigurable configurable)
                {
                    configurable.ConfigApplicationBuilder(app);
                }
            }
        }
    }

    /// <summary>
    /// Maps endpoints contributed by components materialized for the current host.
    /// </summary>
    /// <param name="app">The current host's application builder.</param>
    internal void ConfigureEndpoints(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var component in SnapshotChannels().SelectMany(channel => channel.Pipe.GetComponents()))
        {
            if (component is IApplicationBuilderConfigurable configurable)
            {
                configurable.ConfigEndpoints(app);
            }
        }
    }

    /// <inheritdoc />
    public DataChannel? Fetch(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_syncRoot)
        {
            return _channels.GetValueOrDefault(id);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DataChannel> FetchGroup(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        return SnapshotChannels().Where(channel => channel.Pipe.GroupId == groupId).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<DataChannel> FetchAll()
    {
        return SnapshotChannels();
    }

    private DataChannel[] SnapshotChannels()
    {
        lock (_syncRoot)
        {
            return _channels.Values.ToArray();
        }
    }
}
