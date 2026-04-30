using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Extension methods for configuring Monica's MCP module.
/// </summary>
public static class ModuleMcpBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables Monica MCP server discovery, MCP hosting, and external MCP client cataloging.
        /// </summary>
        /// <param name="action">Optional MCP module configuration action.</param>
        /// <returns>The MCP module guide.</returns>
        public static ModuleMcpGuide AddMcp(Action<ModuleMcpOption>? action = null)
        {
            return new ModuleMcpGuide().Register(action);
        }
    }
}

/// <summary>
/// Discovers Monica-defined MCP servers and exposes them through configured MCP transports.
/// </summary>
[ModuleKey(BuiltInModuleKey.Mcp)]
public sealed class ModuleMcp(ModuleMcpOption option)
    : WebModuleBase<ModuleMcp, ModuleMcpOption, ModuleMcpGuide>(option),
      IBusinessTypeIterator
{
    private readonly List<Type> _mcpTypes = [];

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleXmlDocumentationGuide>().Register();
    }

    /// <inheritdoc />
    public override bool CanDowngradeToNonWebModule()
    {
        return true;
    }

    /// <inheritdoc />
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false }
                && type.IsAssignableTo(typeof(McpServer)))
            {
                _mcpTypes.Add(type);
            }

            yield return type;
        }
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection services)
    {
        foreach (var mcpType in _mcpTypes.Distinct())
        {
            if (!services.Any(descriptor => descriptor.ServiceType == mcpType))
            {
                services.AddSingleton(mcpType);
            }

            services.AddSingleton(typeof(McpServer), sp => (McpServer)sp.GetRequiredService(mcpType));
        }

        services.TryAddSingleton<ILoadedModuleCatalog, ModuleRegistryLoadedModuleCatalog>();
        services.TryAddSingleton<MonicaMcpCatalog>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<ModelContextProtocol.Server.McpServerOptions>, McpServerOptionsConfigurator>());

        var mcpBuilder = services.AddMcpServer();
        if (_mcpTypes.Count > 0)
        {
            mcpBuilder.WithHttpTransport(transportOptions =>
            {
                transportOptions.Stateless = option.McpHttpStateless;
            });
            services.AddHostedService<MonicaStdioMcpHostedService>();
        }

        foreach (var registration in option.McpClientRegistrations)
        {
            services.AddSingleton(registration);
        }
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        var catalog = app.ApplicationServices.GetRequiredService<MonicaMcpCatalog>();
        if (!catalog.HasHttpServers)
        {
            return;
        }

        UseEndpoints(app, endpoints =>
        {
            endpoints.MapMcp(Option.McpHttpEndpointPath);
        });
    }
}

/// <summary>
/// Configuration options for Monica's MCP module.
/// </summary>
public sealed class ModuleMcpOption : ModuleOptions<ModuleMcp>
{
    internal List<McpClientRegistration> McpClientRegistrations { get; } = [];

    /// <summary>
    /// Route pattern used when Monica-defined MCP servers choose HTTP transport.
    /// The ASP.NET Core host remains responsible for binding addresses, ports, and TLS.
    /// </summary>
    public string McpHttpEndpointPath { get; set; } = "/mcp";

    /// <summary>
    /// Optional display URL shown in MCP management UIs when the externally reachable URL differs
    /// from the local route pattern.
    /// </summary>
    public string? McpHttpDisplayUrl { get; set; }

    /// <summary>
    /// Enables stateless Streamable HTTP transport for Monica's MCP endpoint. Stateless mode avoids
    /// server-side MCP session storage and maps only the HTTP POST endpoint.
    /// </summary>
    public bool McpHttpStateless { get; set; } = true;

    /// <summary>
    /// Adds an external MCP client registration to Monica's MCP catalog.
    /// </summary>
    /// <param name="registration">External MCP client registration to add.</param>
    public void AddMcpClient(McpClientRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        McpClientRegistrations.Add(registration);
    }
}

/// <summary>
/// Configuration guide for Monica's MCP module.
/// </summary>
public sealed class ModuleMcpGuide
    : WebModuleGuide<ModuleMcp, ModuleMcpOption, ModuleMcpGuide>
{
    /// <summary>
    /// Configures the HTTP route used by Monica-defined MCP servers that select HTTP transport.
    /// </summary>
    /// <param name="endpointPath">ASP.NET Core route pattern for the MCP endpoint. Defaults to <c>"/mcp"</c>.</param>
    /// <param name="displayUrl">Optional externally reachable URL shown in future management UIs.</param>
    /// <param name="stateless">Whether Streamable HTTP should use stateless mode.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleMcpGuide ConfigureMcpHttpEndpoint(
        string endpointPath = "/mcp",
        string? displayUrl = null,
        bool stateless = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);

        ConfigureModuleOption(options =>
        {
            options.McpHttpEndpointPath = endpointPath;
            options.McpHttpDisplayUrl = displayUrl;
            options.McpHttpStateless = stateless;
        });
        return this;
    }

    /// <summary>
    /// Registers an external MCP client whose tools may be exposed to Monica agents.
    /// </summary>
    /// <param name="name">Stable client name shown to management UIs.</param>
    /// <param name="description">Short description of the remote MCP server or client connection.</param>
    /// <param name="clientFactory">Factory that creates and connects an MCP client when tools are first needed.</param>
    /// <param name="isAgentToolEnabled">Whether listed client tools should be exposed to Monica agents.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleMcpGuide AddMcpClient(
        string name,
        string description,
        Func<IServiceProvider, CancellationToken, Task<McpClient>> clientFactory,
        bool isAgentToolEnabled = true)
    {
        var registration = new McpClientRegistration(name, description, clientFactory)
        {
            IsAgentToolEnabled = isAgentToolEnabled
        };

        ConfigureModuleOption(options => options.AddMcpClient(registration), secondKey: registration.Name);
        return this;
    }
}
