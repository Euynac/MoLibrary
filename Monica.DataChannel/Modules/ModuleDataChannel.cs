using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.DataChannel;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Facades;
using Monica.DataChannel.Metrics;
using Monica.DataChannel.Providers.TCP.Utils;
using Monica.DataChannel.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Registers host-scoped data-channel composition, management, initialization, and endpoints.
/// </summary>
[ModuleKey(BuiltInModuleKey.DataChannel)]
public class ModuleDataChannel(ModuleDataChannelOption option)
    : WebModuleBase<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<ModuleDataChannelOption>()
            .Validate(
                static options => options.RecentExceptionToKeep > 0,
                $"{nameof(ModuleDataChannelOption.RecentExceptionToKeep)} must be greater than zero.")
            .Validate(
                static options => options.InitThreadCount > 0,
                $"{nameof(ModuleDataChannelOption.InitThreadCount)} must be greater than zero.")
            .ValidateOnStart();
        services.TryAddSingleton<DataChannelRuntime>();
        services.TryAddSingleton<IDataChannelRegistrar>(provider => provider.GetRequiredService<DataChannelRuntime>());
        services.TryAddSingleton<IDataChannelManager>(provider => provider.GetRequiredService<DataChannelRuntime>());
        services.TryAddSingleton<TcpConnectionRuntime>();
        services.TryAddSingleton<MessageMetrics>();
        services.AddScoped<DataChannelFacade>();
        services.AddHostedService<DataChannelInitializerService>();
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        var setup = app.ApplicationServices.GetRequiredService<IDataChannelSetup>();
        var registrar = app.ApplicationServices.GetRequiredService<IDataChannelRegistrar>();
        var runtime = app.ApplicationServices.GetRequiredService<DataChannelRuntime>();

        setup.Setup(registrar);
        runtime.Materialize(app);
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.ApplicationServices.GetRequiredService<DataChannelRuntime>().ConfigureEndpoints(app);

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapPost("/channel/{id}/reinitialize",
                async ([FromRoute] string id,
                      [FromServices] DataChannelFacade service,
                      CancellationToken cancellationToken = default) =>
                {
                    var result = await service.ReInitializeChannelAsync(id, cancellationToken);
                    return result.GetResponse();
                })
                .WithName("DataChannel.Reinitialize")
                .WithTags(tagName)
                .WithSummary("Reinitialize a data channel")
                .WithDescription("Reinitializes the data channel with the specified identifier.");

            endpoints.MapGet("/channels",
                async ([FromServices] DataChannelFacade service) =>
                {
                    var result = await service.GetChannelsStatusAsync();
                    return result.GetResponse();
                })
                .WithName("DataChannel.List")
                .WithTags(tagName)
                .WithSummary("List data-channel status")
                .WithDescription("Returns a status snapshot for every data channel owned by the current host.");

            endpoints.MapGet("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromQuery] int count,
                      [FromServices] DataChannelFacade service) =>
                {
                    var result = await service.GetChannelExceptionsAsync(id, count);
                    return result.GetResponse();
                })
                .WithName("DataChannel.GetExceptions")
                .WithTags(tagName)
                .WithSummary("Get data-channel exceptions")
                .WithDescription("Returns recent exception records for the specified data channel.");

            endpoints.MapGet("/channels/exceptions/summary",
                async ([FromServices] DataChannelFacade service) =>
                {
                    var result = await service.GetExceptionSummaryAsync();
                    return result.GetResponse();
                })
                .WithName("DataChannel.GetExceptionSummary")
                .WithTags(tagName)
                .WithSummary("Get the data-channel exception summary")
                .WithDescription("Returns aggregate exception statistics for data channels owned by the current host.");

            endpoints.MapDelete("/channel/{id}/exceptions",
                async ([FromRoute] string id,
                      [FromServices] DataChannelFacade service) =>
                {
                    var result = await service.ClearChannelExceptionsAsync(id);
                    return result.GetResponse();
                })
                .WithName("DataChannel.ClearExceptions")
                .WithTags(tagName)
                .WithSummary("Clear data-channel exceptions")
                .WithDescription("Clears exception history for the specified data channel.");
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleHostedServiceGuide>().Register();
    }
}

public static class ModuleDataChannelBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Adds the DataChannel module to the current Monica host.
        /// </summary>
        /// <param name="action">An optional callback that configures retention, initialization, and Minimal API options.</param>
        /// <returns>A guide used to register the host's required channel setup.</returns>
        public ModuleDataChannelGuide AddDataChannel(Action<ModuleDataChannelOption>? action = null)
        {
            return builder.AddModule<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>(action);
        }
    }
}

/// <summary>
/// Configures host-specific DataChannel pipeline declarations.
/// </summary>
public class ModuleDataChannelGuide : WebModuleGuide<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>
{
    private const string CHANNEL_SETUP = nameof(CHANNEL_SETUP);

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [CHANNEL_SETUP];
    }

    /// <summary>
    /// Registers the setup that declares all pipelines owned by the current host.
    /// </summary>
    /// <typeparam name="TSetup">
    /// A singleton setup implementation. Its dependencies are resolved from the current host,
    /// and the framework invokes it once before materializing channel pipelines.
    /// </typeparam>
    /// <returns>The current guide.</returns>
    public ModuleDataChannelGuide UseSetup<TSetup>()
        where TSetup : class, IDataChannelSetup
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IDataChannelSetup, TSetup>();
        }, key: CHANNEL_SETUP);
        return this;
    }
}

/// <summary>
/// Configuration options for the DataChannel module.
/// Defines host-specific settings and module-level behavior for data channels.
/// </summary>
public class ModuleDataChannelOption : MinimalApiModuleOptions<ModuleDataChannel>
{
    /// <summary>
    /// Gets or sets how many recent exceptions each channel retains. The default is 10 and the value must be positive.
    /// </summary>
    public int RecentExceptionToKeep { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of channels initialized concurrently. The default is 10 and the value must be positive.
    /// </summary>
    public int InitThreadCount { get; set; } = 10;
}
