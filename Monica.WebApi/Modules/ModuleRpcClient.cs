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
        var registeredInterfaces = new HashSet<Type>();
        var registeredAppIds = new HashSet<string>();
        var infoProvider = Option.DomainInfoProvider;
        if (infoProvider == null) throw new Exception("You must config DomainInfoProvider to use rpc client!");
        var dependentDomains = infoProvider.GetDependencyDomains() as Enum;
        foreach (var enumValue in Enum.GetValues(dependentDomains!.GetType()))
        {
            if (enumValue is Enum domain && dependentDomains.HasFlag(domain))
            {
                if (domain.ToString() == "None") continue;

                foreach (var type in RelatedTypes.Where(p => p.Name.IndexOf("Api", StringComparison.Ordinal) is var index and > 0 
                                                             && domain.ToString() == p.Name[(index + 3)..]))
                {
                    var interfaces = type.GetInterfaces()
                        .Where(p => p != typeof(IRpcApi) && p.IsImplementInterface<IRpcApi>()).ToList();
                    switch (interfaces.Count)
                    {
                        case 0:
                            continue;
                        case > 1:
                            throw new InvalidOperationException(
                                $"There are multiple interfaces ({interfaces.Select(p => p.GetCleanFullName()).StringJoin(",")}) extend {nameof(IRpcApi)} for type {type.GetCleanFullName()}");
                    }

                    var targetInterface = interfaces[0];
                    if (!registeredInterfaces.Add(targetInterface))
                    {
                        throw new InvalidOperationException(
                            $"Interface {targetInterface.GetCleanFullName()} has been registered, the type {type.GetCleanFullName()} can not register again!");
                    }

                    if (type.IsSubclassOf(typeof(HttpRpcApi)))
                    {
                        if (Option.HttpClientRegisterProviderType is not { } httpClientRegisterProviderType)
                        {
                            throw new InvalidOperationException(
                                "Please config MoRPC http client provider to use rpc client!");
                        }

                        var appid = infoProvider.GetDomainRelatedAppId(domain);

                        if (registeredAppIds.Add(appid))
                        {
                            var httpClientBuilder = services.AddHttpClient(appid);
                            httpClientBuilder.AddHttpMessageHandler<AuthenticationDelegatingHandler>();
                            services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>>(provider =>
                                new ConfigureNamedOptions<HttpClientFactoryOptions>(appid, options =>
                                {
                                    var httpClientRegisterProvider = (IRpcHttpClientRegisterProvider)provider.GetRequiredService(httpClientRegisterProviderType);
                                    httpClientRegisterProvider.ConfigureHttpClientFactoryOptions(options, appid);
                                }));
                        }

                        services.TryAddTransient(targetInterface, provider =>
                        {
                            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
                            var client = httpClientFactory.CreateClient(appid);
                            return ActivatorUtilities.CreateInstance(provider, type, client);
                        });
                        Logger.LogInformation("Register Domain ({domainName} - {domainDesc}) HTTP RPC {type} -> {interface}", domain.ToString(), domain.GetDescription(), type.Name,
                            targetInterface.Name);
                    }
                    else if (Option.UseGrpc)
                    {
                        throw new NotImplementedException("Grpc is not supported now!");
                    }
                    else
                    {
                        services.TryAddTransient(targetInterface, type);
                        Logger.LogInformation("Register Domain ({domainName} - {domainDesc}) Custom RPC {type} -> {interface}", domain.ToString(), domain.GetDescription(), type.Name,
                            targetInterface.Name);
                    }
                }
            }
        }
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAuthenticationGuide>().Register().ConfigDefaultSystemUser();
    }
}

public class ModuleRpcClientGuide : ModuleGuide<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(ConfigDomainInfoProvider), nameof(ConfigHttpClientRegisterProvider)];
    }

    public ModuleRpcClientGuide ConfigHttpClientRegisterProvider<THttpClientRegisterProvider>()
        where THttpClientRegisterProvider : class, IRpcHttpClientRegisterProvider
    {
        ConfigureModuleOption(option =>
        {
            option.HttpClientRegisterProviderType = typeof(THttpClientRegisterProvider);
        });
        return this;
    }
    
    public ModuleRpcClientGuide ConfigDomainInfoProvider(IRpcClientDomainInfoProvider domainInfoProvider)
    {
        ConfigureModuleOption(option =>
        {
            option.DomainInfoProvider = domainInfoProvider;
        });
        return this;
    }
}

public class ModuleRpcClientOption : ModuleOptions<ModuleRpcClient>
{
    /// <summary>
    /// Registers RPC clients through gRPC instead of HttpClient.
    /// </summary>
    public bool UseGrpc { get; set; }

    internal Type? HttpClientRegisterProviderType { get; set; }
    internal IRpcClientDomainInfoProvider? DomainInfoProvider { get; set; }
}
