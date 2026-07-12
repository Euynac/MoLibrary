using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using Monica.AI.Mcp.Abstractions;
using Monica.AI.Mcp.Facades;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;
using Monica.AI.Mcp.Services;
using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.Abstractions;
using Monica.AI.Services.Support.ModuleCatalog;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Skills;

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
    private readonly List<Type> _skillTypes = [];

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleXmlDocumentationGuide>().Register();
        DependsOnModule<ModuleAIGuide>().Register();
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

            if (type is { IsClass: true, IsAbstract: false }
                && type.IsAssignableTo(typeof(Skill)))
            {
                _skillTypes.Add(type);
            }

            yield return type;
        }
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection services)
    {
        foreach (var mcpType in _mcpTypes.Distinct())
        {
            if (services.All(descriptor => descriptor.ServiceType != mcpType))
            {
                services.AddSingleton(mcpType);
            }

            services.AddSingleton(typeof(McpServer), sp => (McpServer)sp.GetRequiredService(mcpType));
        }

        services.TryAddSingleton<ILoadedModuleCatalog, ModuleRegistryLoadedModuleCatalog>();
        services.TryAddSingleton<MonicaMcpCatalog>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAgentCapabilitySource, McpAgentCapabilitySource>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAIChatAgentContributor, McpChatAgentContributor>());
        services.AddScoped(sp => new McpFacade(
            sp.GetRequiredService<IExternalMcpClientProfileStore>(),
            sp.GetRequiredService<MonicaMcpCatalog>(),
            sp.GetRequiredService<IAgentCapabilityService>()));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<ModelContextProtocol.Server.McpServerOptions>, McpServerOptionsConfigurator>());

        var mcpBuilder = services.AddMcpServer();
        if (_mcpTypes.Count > 0 || _skillTypes.Count > 0)
        {
            mcpBuilder.WithHttpTransport(transportOptions =>
            {
                transportOptions.Stateless = option.McpHttpStateless;
                transportOptions.ConfigureSessionOptions = (httpContext, serverOptions, _) =>
                {
                    var catalog = httpContext.RequestServices.GetRequiredService<MonicaMcpCatalog>();
                    var serverName = httpContext.Request.RouteValues[ModuleMcpOption.HTTP_SERVER_ROUTE_VALUE]?.ToString();
                    if (!catalog.TryConfigureHttpServerOptions(serverName, serverOptions))
                    {
                        throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(serverName)
                                ? "HTTP MCP server name is required in the endpoint route."
                                : $"HTTP MCP server '{serverName}' was not found or is not configured for HTTP transport.");
                    }

                    return Task.CompletedTask;
                };
            });
            services.AddHostedService<MonicaStdioMcpHostedService>();
        }

        services.TryAddSingleton<IExternalMcpClientProfileStore, FileExternalMcpClientProfileStore>();
        services.TryAddSingleton<ExternalMcpClientFactory>();

        foreach (var profile in option.ExternalMcpClientProfiles)
        {
            services.AddSingleton(profile);
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
            endpoints.MapMcp(Option.CreateHttpEndpointRoutePattern())
                .WithMonicaEndpoint();
        });
    }
}

/// <summary>
/// Configuration options for Monica's MCP module.
/// </summary>
public sealed class ModuleMcpOption : ModuleOptions<ModuleMcp>
{
    internal const string HTTP_SERVER_ROUTE_VALUE = "serverName";

    internal List<ExternalMcpClientProfile> ExternalMcpClientProfiles { get; } = [];

    /// <summary>
    /// Base route pattern used when Monica-defined MCP servers choose HTTP transport.
    /// Monica appends a <c>{serverName}</c> route segment so each logical HTTP MCP server owns an isolated tool namespace.
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
    /// Relative or absolute file path used to persist runtime-managed external MCP client profiles.
    /// Defaults to <c>monica_data/ai/external_mcp_clients.json</c>.
    /// </summary>
    public string ExternalMcpClientProfileStoreFilePath { get; set; } = "monica_data/ai/external_mcp_clients.json";

    /// <summary>
    /// Adds a code-defined external MCP client profile to Monica's MCP catalog.
    /// </summary>
    /// <param name="profile">External MCP client profile to add.</param>
    public void AddMcpClient(ExternalMcpClientProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = profile.Normalize(ExternalMcpClientProfileOrigin.Code);
        normalized.Validate();
        ExternalMcpClientProfiles.Add(normalized);
    }

    internal string CreateHttpEndpointRoutePattern()
    {
        return AppendServerNameSegment(NormalizeHttpEndpointBasePath(McpHttpEndpointPath), rawServerName: false);
    }

    internal static string CreateHttpEndpointPath(string basePath, string serverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        return AppendServerNameSegment(NormalizeHttpEndpointBasePath(basePath), serverName);
    }

    internal static string? CreateHttpDisplayUrl(string? displayBaseUrl, string serverName)
    {
        if (string.IsNullOrWhiteSpace(displayBaseUrl))
        {
            return null;
        }

        var normalizedBaseUrl = displayBaseUrl.TrimEnd('/');
        return $"{normalizedBaseUrl}/{Uri.EscapeDataString(serverName.Trim())}";
    }

    private static string NormalizeHttpEndpointBasePath(string endpointPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointPath);

        var path = endpointPath.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.StartsWith('/') ? path : "/" + path;
    }

    private static string AppendServerNameSegment(string basePath, string? serverName = null, bool rawServerName = true)
    {
        var segment = rawServerName
            ? Uri.EscapeDataString(serverName ?? string.Empty)
            : $"{{{HTTP_SERVER_ROUTE_VALUE}}}";

        return string.IsNullOrWhiteSpace(basePath)
            ? "/" + segment
            : basePath + "/" + segment;
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
    /// <param name="endpointPath">ASP.NET Core base route pattern for HTTP MCP endpoints. Defaults to <c>"/mcp"</c>. Monica appends <c>/{serverName}</c>.</param>
    /// <param name="displayUrl">Optional externally reachable base URL shown in management UIs. Monica appends each MCP server name.</param>
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
    /// Registers an external HTTP MCP client whose tools may be exposed to Monica agents.
    /// </summary>
    /// <param name="name">Stable client name shown to management UIs.</param>
    /// <param name="description">Short description of the remote MCP server or client connection.</param>
    /// <param name="endpoint">Absolute HTTP or HTTPS endpoint for the remote MCP server.</param>
    /// <param name="headers">Optional HTTP headers sent with MCP requests.</param>
    /// <param name="transportMode">HTTP transport mode used by the MCP SDK.</param>
    /// <param name="connectionTimeoutSeconds">Connection timeout in seconds.</param>
    /// <param name="isAgentToolEnabled">Whether listed client tools should be exposed to Monica agents.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleMcpGuide AddMcpClient(
        string name,
        string description,
        string endpoint,
        IDictionary<string, string>? headers = null,
        HttpTransportMode transportMode = HttpTransportMode.AutoDetect,
        int connectionTimeoutSeconds = 30,
        bool isAgentToolEnabled = true)
    {
        var profile = new ExternalMcpClientProfile
        {
            Name = name,
            Description = description,
            Endpoint = endpoint,
            Headers = new Dictionary<string, string>(headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            TransportMode = transportMode,
            ConnectionTimeoutSeconds = connectionTimeoutSeconds,
            IsAgentToolEnabled = isAgentToolEnabled,
            Origin = ExternalMcpClientProfileOrigin.Code
        }.Normalize(ExternalMcpClientProfileOrigin.Code);

        ConfigureModuleOption(options => options.AddMcpClient(profile), secondKey: profile.Name);
        return this;
    }
}
