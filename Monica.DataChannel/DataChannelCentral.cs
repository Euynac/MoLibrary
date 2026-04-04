using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions;
using Monica.Modules;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel;

/// <summary>
/// Central coordinator for the messaging channel infrastructure.
/// Manages all data channels and provides unified registration, access, and configuration behavior.
/// </summary>
public static class DataChannelCentral
{
    private static ModuleDataChannelOption? _setting;

    /// <summary>
    /// Gets or sets the global configuration for the data channel module.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the settings have not been initialized.</exception>
    internal static ModuleDataChannelOption Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(DataChannelCentral)}. Please register DataExchange first");
        set => _setting = value;
    }

    /// <summary>
    /// Gets the global logger.
    /// </summary>
    internal static ILogger Logger => Setting.Logger;

    /// <summary>
    /// Gets all registered data channels keyed by channel identifier.
    /// </summary>
    public static Dictionary<string, DataChannel> Channels { get; } = [];

    /// <summary>
    /// Gets the collection of registered data pipeline builders.
    /// </summary>
    internal static List<ChannelPipelineBuilder> Builders { get; } = [];

    /// <summary>
    /// Registers a configured data pipeline with the central manager.
    /// </summary>
    /// <param name="pipe">The data pipeline to register.</param>
    public static void RegisterPipeline(ChannelPipeline pipe)
    {
        Channels.Add(pipe.Id, new DataChannel(pipe));
    }

    /// <summary>
    /// Registers a data pipeline builder.
    /// </summary>
    /// <param name="builder">The data pipeline builder to register.</param>
    internal static void RegisterBuilder(ChannelPipelineBuilder builder)
    {
        Builders.Add(builder);
    }

    /// <summary>
    /// Builds all data pipelines from the registered builders.
    /// Also applies application-level configuration for components that support dynamic ASP.NET Core setup.
    /// </summary>
    /// <param name="app">The application builder instance.</param>
    internal static void StartBuild(IApplicationBuilder app)
    {
        foreach (var builder in Builders)
        {
            var pipe = builder.Build(app.ApplicationServices);
            foreach (var component in pipe.GetComponents())
            {
                if (component is IApplicationBuilderConfigurable config)
                    config.ConfigApplicationBuilder(app);
            }
        }
    }
    /// <summary>
    /// Configures endpoints for all registered data channel components that support dynamic endpoint setup.
    /// </summary>
    /// <param name="app">The application builder instance.</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        foreach (var component in Channels.Values.SelectMany(p => p.Pipe.GetComponents()))
        {
            if (component is IApplicationBuilderConfigurable config)
                config.ConfigEndpoints(app);
        }
    }
}
