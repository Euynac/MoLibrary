using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.AutoController.MoRpc;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DomainDrivenDesign.Modules;

public static class ModuleRpcClientBuilderExtensions
{
    public static ModuleRpcClientGuide ConfigModuleRpcClient(this WebApplicationBuilder builder,
        Action<ModuleRpcClientOption>? action = null)
    {
        return new ModuleRpcClientGuide().Register(action);
    }
}

public class ModuleRpcClient(ModuleRpcClientOption option) :
    MoModuleWithDependencies<ModuleRpcClient, ModuleRpcClientOption, ModuleRpcClientGuide>(option),
    IWantIterateBusinessTypes
{
    public List<Type> RelatedTypes { get; set; } = [];

    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RpcClient;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (!type.IsAbstract && type.IsSubclassOf(typeof(MoRpcApi)))
            {
                RelatedTypes.Add(type);
            }

            yield return type;
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        var registeredInterfaces = new HashSet<Type>();
        var infoProvider = Option.DomainInfoProvider;
        if (infoProvider == null) throw new Exception("You must config DomainInfoProvider to use rpc client!");
        var dependentDomains = infoProvider.GetDependencyDomains() as Enum;
        foreach (var enumValue in Enum.GetValues(dependentDomains!.GetType()))
        {
            if (enumValue is Enum domain && dependentDomains.HasFlag(domain))
            {
                if (domain.ToString() == "None") continue;

                foreach (var type in RelatedTypes.Where(p => p.Name.Contains(domain.ToString())))
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
                            $"Interface {targetInterface.GetCleanFullName()} has been registered for type {type.GetCleanFullName()}");
                    }

                    if (type.IsSubclassOf(typeof(MoHttpApi)))
                    {
                        if (Option.HttpClientRegisterProvider is not { } httpClientRegisterProvider)
                        {
                            throw new InvalidOperationException(
                                "Please config MoRPC http client provider to use rpc client!");
                        }

                        services.TryAddSingleton(targetInterface, provider =>
                        {
                            var client =
                                httpClientRegisterProvider.GetHttpClient(
                                    infoProvider.GetDomainRelatedAppId(domain));
                            return ActivatorUtilities.CreateInstance(provider, type, client);
                        });
                        Logger.LogInformation("Register Domain ({domainName} - {domainDesc}) HTTP RPC {type} for {interface}", domain.ToString(), domain.GetDescription(), type.GetCleanFullName(),
                            targetInterface.GetCleanFullName());
                    }
                    else if (Option.UseGrpc)
                    {
                        continue;
                    }
                    else
                    {
                        services.TryAddSingleton(targetInterface, type);
                        Logger.LogInformation("Register Custom RPC {type} for {interface}", type.GetCleanFullName(),
                            targetInterface.GetCleanFullName());
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

    public ModuleRpcClientGuide ConfigHttpClientRegisterProvider(IMoRpcHttpClientRegisterProvider httpClientRegisterProvider)
    {
        ConfigureModuleOption(option =>
        {
            option.HttpClientRegisterProvider = httpClientRegisterProvider;
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

    internal IMoRpcHttpClientRegisterProvider? HttpClientRegisterProvider { get; set; }
    internal IMoRpcClientDomainInfoProvider? DomainInfoProvider { get; set; }
}

public interface IMoRpcClientDomainInfoProvider
{
    object GetDependencyDomains();

    string GetDomainRelatedAppId(Enum domain);
}

public interface IMoRpcHttpClientRegisterProvider
{
    HttpClient GetHttpClient(string appid);
}