using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Tool.Extensions;
using Monica.WebApi.RpcClient.Abstractions;
using Monica.WebApi.RpcClient.Annotations;
using Monica.WebApi.RpcClient.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRpcClientBuilderExtensions
{
    internal const string DOMAIN_PROVIDER_FEATURE = "rpc-domain-provider";

    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers and configures the RPC client module.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> AddRpcClient(
            Action<ModuleRpcClientOption>? action = null)
        {
            return builder.AddModule<ModuleRpcClient, ModuleRpcClientOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> registration)
    {
        /// <summary>
        /// Selects HTTP transport implementations for RPC contracts.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> UseHttpTransport()
        {
            return registration.Configure(options => options.Transport = RpcTransportKind.Http);
        }

        /// <summary>
        /// Selects in-process transport implementations for RPC contracts.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> UseLocalTransport()
        {
            return registration.Configure(options => options.Transport = RpcTransportKind.Local);
        }

        /// <summary>
        /// Selects the reserved gRPC transport, which currently fails validation at composition time.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> UseGrpcTransport()
        {
            return registration.Configure(options => options.Transport = RpcTransportKind.Grpc);
        }

        /// <summary>
        /// Selects the provider responsible for named HTTP-client registration.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> ConfigHttpClientRegisterProvider<TProvider>()
            where TProvider : class, IRpcHttpClientRegisterProvider
        {
            return registration.Configure(options => options.HttpClientRegisterProviderType = typeof(TProvider));
        }

        /// <summary>
        /// Configures the dependency-domain resolver used to select generated clients.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> ConfigDomainInfoProvider(
            IRpcClientDomainInfoProvider domainInfoProvider)
        {
            ArgumentNullException.ThrowIfNull(domainInfoProvider);
            return registration
                .Configure(options => options.DomainInfoProvider = domainInfoProvider)
                .SatisfyFeature(DOMAIN_PROVIDER_FEATURE);
        }

        /// <summary>
        /// Applies additional HTTP-client customization to every HTTP RPC registration.
        /// </summary>
        public ModuleRegistration<ModuleRpcClient, ModuleRpcClientOption> ConfigHttpClientBuilder(
            Action<IHttpClientBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return registration.Configure(options => options.CustomHttpClientBuilder = configure);
        }
    }
}

public class ModuleRpcClient : MonicaModule<ModuleRpcClientOption>
{
    private const string NONE_DOMAIN_NAME = "None";

    private readonly List<BusinessTypeMatch> _relatedTypes = [];

    public override void Describe(ModuleDescriptor module)
    {
        module.RequireFeature(ModuleRpcClientBuilderExtensions.DOMAIN_PROVIDER_FEATURE);
        module.Require<ModuleAuthentication, ModuleAuthenticationOption>();
        module.Require<ModuleResultEnvelope, ModuleResultEnvelopeOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleRpcClientOption> context)
    {
        var services = context.Services;
        services.AddHttpContextAccessor();
        services.AddTransient<AuthenticationDelegatingHandler>();
        if (Option.HttpClientRegisterProviderType is { } providerType)
        {
            services.TryAddSingleton(providerType);
        }
    }

    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleRpcClientOption> discovery)
    {
        discovery.Match(
            TypeQuery.ClosedClass.SubclassOf<RpcApi>(),
            (_, matches) =>
            {
                _relatedTypes.Clear();
                _relatedTypes.AddRange(matches);
            });
    }

    public override void PostConfigureServices(ModuleContext<ModuleRpcClientOption> context)
    {
        var services = context.Services;
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
            .Where(domain => !string.Equals(domain.ToString(), NONE_DOMAIN_NAME, StringComparison.Ordinal))
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

        foreach (var match in _relatedTypes)
        {
            var registration = CreateRpcClientRegistration(match, validDomainNames, domainEnumType);
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
        BusinessTypeMatch match,
        HashSet<string> validDomainNames,
        Type domainEnumType)
    {
        var type = match.Type;
        var domainAttribute = match.Shape.GetAttribute<RpcClientDomainAttribute>(inherit: true)
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
            ResolveRpcInterface(match.Shape),
            ResolveTransportKind(match.Shape));
    }

    private static Type ResolveRpcInterface(BusinessTypeShape shape)
    {
        var interfaces = shape.Interfaces
            .Where(p => p != typeof(IRpcApi) && p.IsImplementInterface<IRpcApi>())
            .ToList();

        return interfaces.Count switch
        {
            1 => interfaces[0],
            0 => throw new InvalidOperationException(
                $"RPC client type {shape.Type.GetCleanFullName()} must implement exactly one interface extending {nameof(IRpcApi)}."),
            _ => throw new InvalidOperationException(
                $"There are multiple interfaces ({interfaces.Select(p => p.GetCleanFullName()).StringJoin(",")}) extend {nameof(IRpcApi)} for type {shape.Type.GetCleanFullName()}")
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

    private static RpcTransportKind? ResolveTransportKind(BusinessTypeShape shape)
    {
        if (shape.IsSubclassOf(typeof(HttpRpcApi)))
        {
            return RpcTransportKind.Http;
        }

        if (shape.IsSubclassOf(typeof(LocalRpcApi)))
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

public class ModuleRpcClientOption : ModuleOptions<ModuleRpcClient>
{
    /// <summary>
    /// Selects which generated RPC transport implementation should be registered.
    /// Defaults to <see cref="RpcTransportKind.Http"/> for distributed service-to-service calls.
    /// </summary>
    public RpcTransportKind Transport { get; set; } = RpcTransportKind.Http;

    /// <summary>
    /// Applies additional customization to HTTP client registrations when <see cref="Transport"/> is <see cref="RpcTransportKind.Http"/>.
    /// </summary>
    public Action<IHttpClientBuilder>? CustomHttpClientBuilder { get; internal set; }

    internal Type? HttpClientRegisterProviderType { get; set; }
    internal IRpcClientDomainInfoProvider? DomainInfoProvider { get; set; }
}
