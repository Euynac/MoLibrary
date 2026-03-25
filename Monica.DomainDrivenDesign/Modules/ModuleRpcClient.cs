using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Abstractions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DomainDrivenDesign.AutoController.MoRpc;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleRpcClientBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 RpcClient 模块
        /// </summary>
        public static ModuleRpcClientGuide AddRpcClient(Action<ModuleRpcClientOption>? action = null)
        {
            return new ModuleRpcClientGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.RpcClient)]
public class ModuleRpcClient(ModuleRpcClientOption option) :
    MoModule<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>(option),
    IWantIterateBusinessTypes
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
            if (type is { IsClass: true, IsAbstract: false} && type.IsSubclassOf(typeof(MoRpcApi)))
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
                        .Where(p => p != typeof(IMoRpcApi) && p.IsImplementInterface<IMoRpcApi>()).ToList();
                    switch (interfaces.Count)
                    {
                        case 0:
                            continue;
                        case > 1:
                            throw new InvalidOperationException(
                                $"There are multiple interfaces ({interfaces.Select(p => p.GetCleanFullName()).StringJoin(",")}) extend {nameof(IMoRpcApi)} for type {type.GetCleanFullName()}");
                    }

                    var targetInterface = interfaces[0];
                    if (!registeredInterfaces.Add(targetInterface))
                    {
                        throw new InvalidOperationException(
                            $"Interface {targetInterface.GetCleanFullName()} has been registered, the type {type.GetCleanFullName()} can not register again!");
                    }

                    if (type.IsSubclassOf(typeof(MoHttpApi)))
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
                                    var httpClientRegisterProvider = (IMoRpcHttpClientRegisterProvider)provider.GetRequiredService(httpClientRegisterProviderType);
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
        DependsOnModule<ModuleDomainDrivenDesignGuide>().Register();
    }
}

public class ModuleRpcClientGuide : MoModuleGuide<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>
{
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(ConfigDomainInfoProvider), nameof(ConfigHttpClientRegisterProvider)];
    }

    public ModuleRpcClientGuide ConfigHttpClientRegisterProvider<THttpClientRegisterProvider>()
        where THttpClientRegisterProvider : class, IMoRpcHttpClientRegisterProvider
    {
        ConfigureModuleOption(option =>
        {
            option.HttpClientRegisterProviderType = typeof(THttpClientRegisterProvider);
        });
        return this;
    }
    
    public ModuleRpcClientGuide ConfigDomainInfoProvider(IMoRpcClientDomainInfoProvider domainInfoProvider)
    {
        ConfigureModuleOption(option =>
        {
            option.DomainInfoProvider = domainInfoProvider;
        });
        return this;
    }
}

public class ModuleRpcClientOption : MoModuleOption<ModuleRpcClient>
{
    /// <summary>
    /// 实现使用Grpc Client进行注册，默认使用HttpClient
    /// </summary>
    public bool UseGrpc { get; set; }

    internal Type? HttpClientRegisterProviderType { get; set; }
    internal IMoRpcClientDomainInfoProvider? DomainInfoProvider { get; set; }
}

public interface IMoRpcClientDomainInfoProvider
{
    object GetDependencyDomains();

    string GetDomainRelatedAppId(Enum domain);
}

public interface IMoRpcHttpClientRegisterProvider
{
    void ConfigureHttpClientFactoryOptions(HttpClientFactoryOptions options, string appid);
}

public class AuthenticationDelegatingHandler(IHttpContextAccessor httpContextAccessor, IMoSystemUserManager systemUserManager) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. 获取当前 HttpContext
        var context = httpContextAccessor.HttpContext;

        if (context != null)//请求从前端发起
        {
            if (context.Request.Headers.Authorization is { } authorization && !string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                var authorizationValue = authorization.ToString();
                if (AuthenticationHeaderValue.TryParse(authorizationValue, out var parsedAuthorization))
                {
                    request.Headers.Authorization = parsedAuthorization;
                }
                else
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationValue.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase));
                }
            }

            else if (await context.GetTokenAsync("access_token") is { } token && !string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        else //请求从后端发起
        {
            var token = systemUserManager.GetTokenOfCurSystemUser();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 4. 继续执行请求
        return await base.SendAsync(request, cancellationToken);
    }
}
