using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Authority.Identity.Abstractions;
using Monica.Core;
using Monica.Core.JsonSerialization.Extensions;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.SignalR.Abstractions;
using Monica.SignalR.Facades;
using Monica.SignalR.Metrics;
using Monica.SignalR.Services;
using Monica.SignalR.Services.Support;
using SignalRSwaggerGen;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions used to register the SignalR infrastructure module.
/// </summary>
public static class ModuleSignalRBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the SignalR infrastructure module and applies optional module configuration.
        /// </summary>
        /// <param name="action">Optional module option configuration delegate.</param>
        /// <returns>The host-bound SignalR module registration.</returns>
        public ModuleRegistration<ModuleSignalR, ModuleSignalROption> AddSignalR(Action<ModuleSignalROption>? action = null)
        {
            return builder.AddModule<ModuleSignalR, ModuleSignalROption>(action);
        }
    }
}

/// <summary>
/// Infrastructure module that registers SignalR services, inspection endpoints, and hub metadata tracking.
/// </summary>
public class ModuleSignalR : MonicaModule<ModuleSignalROption>, IWebHostRequiredModule
{
    internal const string SERVICES_FEATURE = "signalr-services";

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJsonSerialization, ModuleJsonSerializationOption>();
        module.RequireFeature(SERVICES_FEATURE);
    }

    public override void ConfigureServices(ModuleContext<ModuleSignalROption> context)
    {
        var services = context.Services;
        services.AddScoped<SignalRFacade>();
        services.AddScoped<SignalRInspectionService>();
        services.AddSingleton<SignalRHubMetadataReader>();
        services.AddSingleton<ISignalRConnectionRegistry, SignalRConnectionRegistry>();
        services.TryAddSingleton<SignalRSendMetrics>();
    }

    public override void ConfigureEndpoints(WebModuleContext<ModuleSignalROption> context)
    {
        var app = context.ApplicationBuilder;
        UseEndpoints(context, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet("/signalr/hubs",
                async ([FromServices] SignalRFacade facade) =>
                {
                    return (await facade.GetHubInfosAsync()).GetResponse();
                })
                .WithName("Get SignalR hub metadata")
                .WithTags(tagName)
                .WithSummary("Gets metadata for all registered SignalR hubs.")
                .WithDescription("Returns every registered SignalR hub with its route, callable methods, and parameter metadata.");

            endpoints.MapGet("/signalr/connected-users",
                async ([FromServices] SignalRFacade facade) =>
                {
                    return (await facade.GetConnectedUsersAsync()).GetResponse();
                })
                .WithName("Get connected SignalR users")
                .WithTags(tagName)
                .WithSummary("Gets all currently connected SignalR users.")
                .WithDescription("Returns current SignalR connections including connection identifiers, user identity information, and claims.");

            endpoints.MapGet("/signalr/send-diagnostics",
                async ([FromServices] SignalRFacade facade) =>
                {
                    return (await facade.GetSendDiagnosticsAsync()).GetResponse();
                })
                .WithName("Get SignalR send diagnostics")
                .WithTags(tagName)
                .WithSummary("Gets Monica-observed SignalR server-to-client send diagnostics.")
                .WithDescription("Returns pending send task counters, durations, and failures observed by Monica's SignalR operator wrappers. The counters are not ASP.NET Core SignalR private transport queue depth.");
        });
    }
}

/// <summary>
/// Registration extensions for the SignalR infrastructure module.
/// </summary>
public static class ModuleSignalRRegistrationExtensions
{
    /// <summary>
    /// Register SignalR and allow additional configuration of HubOptions and JsonHubProtocolOptions.
    /// </summary>
    /// <typeparam name="TIHubOperator">Hub operator abstraction type.</typeparam>
    /// <typeparam name="THubOperator">Hub operator implementation type.</typeparam>
    /// <typeparam name="TContract">Hub contract type exposed to clients.</typeparam>
    /// <typeparam name="TUser">Application user type resolved from each connection.</typeparam>
    /// <param name="configure">Optional HubOptions configuration delegate.</param>
    /// <param name="jsonConfigure">Optional JsonHubProtocolOptions configuration delegate.</param>
    /// <param name="module">The SignalR registration being configured.</param>
    /// <returns>The same SignalR registration for fluent chaining.</returns>
    public static ModuleRegistration<ModuleSignalR, ModuleSignalROption> AddSignalR<TIHubOperator, THubOperator, TContract, TUser>(this ModuleRegistration<ModuleSignalR, ModuleSignalROption> module,
        Action<HubOptions>? configure = null,
        Action<JsonHubProtocolOptions>? jsonConfigure = null)
        where THubOperator : class, ISignalRHubOperator<TContract, TUser>, TIHubOperator
        where TIHubOperator : class, ISignalRHubOperator<TContract, TUser>
        where TContract : class, ISignalRHubContract
        where TUser : ICurrentUser
    {
        module.SatisfyFeature(ModuleSignalR.SERVICES_FEATURE);
        module.ConfigureServices(context =>
        {
            context.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
            context.Services.AddTransient<ISignalRHubOperator<TContract, TUser>, THubOperator>();
            context.Services.AddTransient<TIHubOperator, THubOperator>();

            var signalRBuilder = context.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                configure?.Invoke(options);
            });

            signalRBuilder.AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.CloneFrom(
                    context.Services.GetMonicaJsonSerializerOptions());
                jsonConfigure?.Invoke(options);
            });
        });
        return module;
    }

    /// <summary>
    /// Configures SignalR Swagger generation.
    /// </summary>
    public static ModuleRegistration<ModuleSignalR, ModuleSignalROption> AddSignalRSwagger(this ModuleRegistration<ModuleSignalR, ModuleSignalROption> module, Action<SignalRSwaggerGenOptions> signalROption)
    {
        module.ConfigureServices(context =>
        {
            context.Services.ConfigureSwaggerGen(o =>
            {
                o.AddSignalRSwaggerGen(signalROption);
            });
        });
        return module;
    }

    /// <summary>
    /// Maps a SignalR hub and records its metadata for inspection APIs and the debug UI.
    /// </summary>
    public static ModuleRegistration<ModuleSignalR, ModuleSignalROption> MapSignalRHub<THub>(this ModuleRegistration<ModuleSignalR, ModuleSignalROption> module, [StringSyntax("Route")] string pattern)
        where THub : Hub
    {
        module.Configure(options =>
        {
            options.HubRegistrations.Add(new SignalRHubRegistration(typeof(THub), pattern));
        });

        module.ConfigureEndpoints(context =>
        {
            context.ApplicationBuilder.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<THub>(pattern)
                    .WithMonicaEndpoint(MonicaEndpointKind.Ui);
            });
        });

        return module;
    }

}

/// <summary>
/// Configuration options for the SignalR infrastructure module.
/// </summary>
public class ModuleSignalROption : MinimalApiModuleOptions<ModuleSignalR>
{
    /// <summary>
    /// Gets the mapped hub registrations used by inspection endpoints and the debug UI.
    /// </summary>
    internal List<SignalRHubRegistration> HubRegistrations { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether Monica should capture low-overhead server-to-client send metrics.
    /// </summary>
    /// <remarks>
    /// The counters measure send tasks observed by Monica's SignalR hub operators. They do not expose ASP.NET Core SignalR's
    /// private per-connection transport queues.
    /// </remarks>
    public bool EnableSendMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether send diagnostics should retain explicit connection, group, or user identifiers.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/> to avoid retaining sensitive identifiers in diagnostic snapshots.
    /// Enable this only in trusted diagnostic environments.
    /// </remarks>
    public bool IncludeSendDiagnosticTargetIdentifiers { get; set; }
}

/// <summary>
/// Internal registration record for mapped SignalR hubs.
/// </summary>
internal sealed record SignalRHubRegistration(Type HubType, string HubRoute);
