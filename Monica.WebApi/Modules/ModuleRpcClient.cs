using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Tool.Extensions;
using Monica.WebApi.RpcClient.Abstractions;
using Monica.WebApi.RpcClient.Annotations;
using Monica.WebApi.RpcClient.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRpcClientBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the RPC client module.
        /// </summary>
        public static ModuleRpcClientGuide AddRpcClient(Action<ModuleRpcClientOption>? action = null)
        {
            return new ModuleRpcClientGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.RpcClient)]
public class ModuleRpcClient(ModuleRpcClientOption option) :
    ModuleBase<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>(option),
    IBusinessTypeIterator
{
    private const string NoneDomainName = "None";

    public List<Type> RelatedTypes { get; set; } = [];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<AuthenticationDelegatingHandler>();
        if (Option.HttpClientRegisterProviderType is { } providerType)
        {
            services.TryAddSingleton(providerType);
        }
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false} && type.IsSubclassOf(typeof(RpcApi)))
            {
                RelatedTypes.Add(type);
            }

            yield return type;
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        var infoProvider = GetRequiredDomainInfoProvider();
        var selectedTransport = Option.Transport;

        ValidateTransportConfiguration(selectedTransport);

        var dependentDomains = GetDependentDomains(infoProvider);
        var supportedDomains = GetSupportedDomains(dependentDomains.GetType());
        var rpcClientsByDomain = BuildRpcClientsByDomain(supportedDomains, dependentDomains.GetType());
        var registeredInterfaces = new HashSet<Type>();
        var registeredAppIds = new HashSet<string>();

        foreach (var domain in supportedDomains.Where(dependentDomains.HasFlag))
        {
            if (!rpcClientsByDomain.TryGetValue(domain.ToString(), out var rpcClients))
            {
                continue;
            }

            foreach (var rpcClient in SelectRpcClientsForTransport(rpcClients, selectedTransport))
            {
                RegisterRpcClient(services, infoProvider, domain, rpcClient, registeredInterfaces, registeredAppIds);
            }
        }
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAuthenticationGuide>().Register().ConfigDefaultSystemUser();
    }

    private IRpcClientDomainInfoProvider GetRequiredDomainInfoProvider()
    {
        return Option.DomainInfoProvider
               ?? throw new InvalidOperationException("You must config DomainInfoProvider to use rpc client!");
    }

    private void ValidateTransportConfiguration(RpcTransportKind selectedTransport)
    {
        switch (selectedTransport)
        {
            case RpcTransportKind.Http when Option.HttpClientRegisterProviderType is null:
                throw new InvalidOperationException(
                    "Please configure an RPC HTTP client provider before using HTTP RPC transport.");
            case RpcTransportKind.Grpc:
                throw new NotImplementedException("Grpc is not supported now!");
        }
    }

    private static Enum GetDependentDomains(IRpcClientDomainInfoProvider infoProvider)
    {
        return infoProvider.GetDependencyDomains() as Enum
               ?? throw new InvalidOperationException(
                   $"{nameof(IRpcClientDomainInfoProvider)}.{nameof(IRpcClientDomainInfoProvider.GetDependencyDomains)} must return an enum instance.");
    }

    private static List<Enum> GetSupportedDomains(Type domainEnumType)
    {
        return Enum.GetValues(domainEnumType)
            .OfType<Enum>()
            .Where(domain => !string.Equals(domain.ToString(), NoneDomainName, StringComparison.Ordinal))
            .ToList();
    }

    private Dictionary<string, List<RpcClientRegistration>> BuildRpcClientsByDomain(
        IEnumerable<Enum> supportedDomains,
        Type domainEnumType)
    {
        var validDomainNames = supportedDomains
            .Select(domain => domain.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var rpcClientsByDomain = new Dictionary<string, List<RpcClientRegistration>>(StringComparer.Ordinal);

        foreach (var type in RelatedTypes)
        {
            var registration = CreateRpcClientRegistration(type, validDomainNames, domainEnumType);
            if (!rpcClientsByDomain.TryGetValue(registration.DomainName, out var registrations))
            {
                registrations = [];
                rpcClientsByDomain.Add(registration.DomainName, registrations);
            }

            registrations.Add(registration);
        }

        return rpcClientsByDomain;
    }

    private static RpcClientRegistration CreateRpcClientRegistration(
        Type type,
        HashSet<string> validDomainNames,
        Type domainEnumType)
    {
        var domainAttribute = type.GetCustomAttribute<RpcClientDomainAttribute>(inherit: true)
                              ?? throw new InvalidOperationException(
                                  $"RPC client type {type.GetCleanFullName()} must declare [{nameof(RpcClientDomainAttribute)}] to participate in automatic registration.");
        if (!validDomainNames.Contains(domainAttribute.DomainName))
        {
            throw new InvalidOperationException(
                $"RPC client type {type.GetCleanFullName()} declares domain '{domainAttribute.DomainName}', but it is not defined by dependency enum {domainEnumType.GetCleanFullName()}.");
        }

        return new RpcClientRegistration(
            domainAttribute.DomainName,
            type,
            ResolveRpcInterface(type),
            ResolveTransportKind(type));
    }

    private static Type ResolveRpcInterface(Type type)
    {
        var interfaces = type.GetInterfaces()
            .Where(p => p != typeof(IRpcApi) && p.IsImplementInterface<IRpcApi>())
            .ToList();

        return interfaces.Count switch
        {
            1 => interfaces[0],
            0 => throw new InvalidOperationException(
                $"RPC client type {type.GetCleanFullName()} must implement exactly one interface extending {nameof(IRpcApi)}."),
            _ => throw new InvalidOperationException(
                $"There are multiple interfaces ({interfaces.Select(p => p.GetCleanFullName()).StringJoin(",")}) extend {nameof(IRpcApi)} for type {type.GetCleanFullName()}")
        };
    }

    private void RegisterRpcClient(
        IServiceCollection services,
        IRpcClientDomainInfoProvider infoProvider,
        Enum domain,
        RpcClientRegistration rpcClient,
        HashSet<Type> registeredInterfaces,
        HashSet<string> registeredAppIds)
    {
        if (!registeredInterfaces.Add(rpcClient.InterfaceType))
        {
            throw new InvalidOperationException(
                $"Interface {rpcClient.InterfaceType.GetCleanFullName()} has been registered, the type {rpcClient.ClientType.GetCleanFullName()} can not register again!");
        }

        if (rpcClient.ClientType.IsSubclassOf(typeof(HttpRpcApi)))
        {
            RegisterHttpRpcClient(services, infoProvider, domain, rpcClient, registeredAppIds);
            return;
        }

        services.TryAddTransient(rpcClient.InterfaceType, rpcClient.ClientType);
        Logger.LogInformation(
            "Register Domain ({domainName} - {domainDesc}) {transport} RPC {type} -> {interface}",
            domain.ToString(),
            domain.GetDescription(),
            rpcClient.TransportKind?.ToString() ?? "Custom",
            rpcClient.ClientType.Name,
            rpcClient.InterfaceType.Name);
    }

    private void RegisterHttpRpcClient(
        IServiceCollection services,
        IRpcClientDomainInfoProvider infoProvider,
        Enum domain,
        RpcClientRegistration rpcClient,
        HashSet<string> registeredAppIds)
    {
        if (Option.HttpClientRegisterProviderType is not { } httpClientRegisterProviderType)
        {
            throw new InvalidOperationException("Please config MoRPC http client provider to use rpc client!");
        }

        var appid = infoProvider.GetDomainRelatedAppId(domain);
        if (registeredAppIds.Add(appid))
        {
            var httpClientBuilder = services.AddHttpClient(appid);
            httpClientBuilder.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

            if (Option.CustomHttpClientBuilder is { } method)
            {
                method.Invoke(httpClientBuilder);
            }

            services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>>(provider =>
                new ConfigureNamedOptions<HttpClientFactoryOptions>(appid, options =>
                {
                    var httpClientRegisterProvider = (IRpcHttpClientRegisterProvider)provider.GetRequiredService(httpClientRegisterProviderType);
                    httpClientRegisterProvider.ConfigureHttpClientFactoryOptions(options, appid);
                }));
        }

        services.TryAddTransient(rpcClient.InterfaceType, provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient(appid);
            return ActivatorUtilities.CreateInstance(provider, rpcClient.ClientType, client);
        });

        Logger.LogInformation(
            "Register Domain ({domainName} - {domainDesc}) HTTP RPC {type} -> {interface}",
            domain.ToString(),
            domain.GetDescription(),
            rpcClient.ClientType.Name,
            rpcClient.InterfaceType.Name);
    }

    private static RpcTransportKind? ResolveTransportKind(Type type)
    {
        if (type.IsSubclassOf(typeof(HttpRpcApi)))
        {
            return RpcTransportKind.Http;
        }

        if (type.IsSubclassOf(typeof(LocalRpcApi)))
        {
            return RpcTransportKind.Local;
        }

        return null;
    }

    private static IReadOnlyList<RpcClientRegistration> SelectRpcClientsForTransport(
        IEnumerable<RpcClientRegistration> registrations,
        RpcTransportKind selectedTransport)
    {
        return registrations
            .GroupBy(registration => registration.InterfaceType)
            .Select(group => SelectRpcClientForTransport(group.ToList(), selectedTransport))
            .Where(static registration => registration != null)
            .Cast<RpcClientRegistration>()
            .ToList();
    }

    private static RpcClientRegistration? SelectRpcClientForTransport(
        IReadOnlyList<RpcClientRegistration> registrations,
        RpcTransportKind selectedTransport)
    {
        var transportMatches = registrations
            .Where(registration => registration.TransportKind == selectedTransport)
            .ToList();

        if (transportMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple RPC client implementations match transport '{selectedTransport}' for interface {registrations[0].InterfaceType.GetCleanFullName()}: {DescribeRegistrations(transportMatches)}.");
        }

        if (transportMatches.Count == 1)
        {
            return transportMatches[0];
        }

        var fallbackMatches = registrations
            .Where(registration => registration.TransportKind == null)
            .ToList();

        if (fallbackMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple fallback RPC client implementations were found for interface {registrations[0].InterfaceType.GetCleanFullName()}: {DescribeRegistrations(fallbackMatches)}.");
        }

        return fallbackMatches.Count == 1
            ? fallbackMatches[0]
            : null;
    }

    private static string DescribeRegistrations(IEnumerable<RpcClientRegistration> registrations)
    {
        return registrations
            .Select(registration => $"{registration.ClientType.GetCleanFullName()} [{registration.TransportKind?.ToString() ?? "Custom"}]")
            .StringJoin(", ");
    }

    private sealed record RpcClientRegistration(
        string DomainName,
        Type ClientType,
        Type InterfaceType,
        RpcTransportKind? TransportKind);
}

public class ModuleRpcClientGuide : ModuleGuide<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(ConfigDomainInfoProvider)];
    }

    /// <summary>
    /// Registers HTTP transport implementations for RPC contracts.
    /// </summary>
    public ModuleRpcClientGuide UseHttpTransport()
    {
        ConfigureModuleOption(option =>
        {
            option.Transport = RpcTransportKind.Http;
        });
        return this;
    }

    /// <summary>
    /// Registers in-process local transport implementations for RPC contracts.
    /// </summary>
    public ModuleRpcClientGuide UseLocalTransport()
    {
        ConfigureModuleOption(option =>
        {
            option.Transport = RpcTransportKind.Local;
        });
        return this;
    }

    /// <summary>
    /// Registers gRPC transport implementations for RPC contracts.
    /// </summary>
    public ModuleRpcClientGuide UseGrpcTransport()
    {
        ConfigureModuleOption(option =>
        {
            option.Transport = RpcTransportKind.Grpc;
        });
        return this;
    }

    /// <summary>
    /// Configures the provider responsible for named <see cref="HttpClient"/> registration.
    /// This is required when <see cref="UseHttpTransport"/> is used.
    /// </summary>
    public ModuleRpcClientGuide ConfigHttpClientRegisterProvider<THttpClientRegisterProvider>()
        where THttpClientRegisterProvider : class, IRpcHttpClientRegisterProvider
    {
        ConfigureModuleOption(option =>
        {
            option.HttpClientRegisterProviderType = typeof(THttpClientRegisterProvider);
        });
        return this;
    }

    /// <summary>
    /// Configures the dependency-domain resolver used to match generated RPC clients to the current service.
    /// </summary>
    public ModuleRpcClientGuide ConfigDomainInfoProvider(IRpcClientDomainInfoProvider domainInfoProvider)
    {
        ConfigureModuleOption(option =>
        {
            option.DomainInfoProvider = domainInfoProvider;
        });
        return this;
    }

    /// <summary>
    /// Applies additional HTTP client customization for HTTP RPC registrations.
    /// </summary>
    public ModuleRpcClientGuide ConfigHttpClientBuilder(Action<IHttpClientBuilder> builderFunc)
    {
        ConfigureModuleOption(option =>
        {
            option.CustomHttpClientBuilder = builderFunc;
        });
        return this;
    }
}

public class ModuleRpcClientOption : ModuleOptions<ModuleRpcClient>
{
    /// <summary>
    /// Selects which generated RPC transport implementation should be registered.
    /// Defaults to <see cref="RpcTransportKind.Http"/> for distributed service-to-service calls.
    /// </summary>
    public RpcTransportKind Transport { get; set; } = RpcTransportKind.Http;

    /// <summary>
    /// Backward-compatible switch for gRPC transport selection.
    /// New code should configure <see cref="Transport"/> or the guide transport methods directly.
    /// </summary>
    public bool UseGrpc
    {
        get => Transport == RpcTransportKind.Grpc;
        set
        {
            if (value)
            {
                Transport = RpcTransportKind.Grpc;
            }
            else if (Transport == RpcTransportKind.Grpc)
            {
                Transport = RpcTransportKind.Http;
            }
        }
    }

    /// <summary>
    /// Applies additional customization to HTTP client registrations when <see cref="Transport"/> is <see cref="RpcTransportKind.Http"/>.
    /// </summary>
    public Action<IHttpClientBuilder>? CustomHttpClientBuilder { get; internal set; }

    internal Type? HttpClientRegisterProviderType { get; set; }
    internal IRpcClientDomainInfoProvider? DomainInfoProvider { get; set; }
}
