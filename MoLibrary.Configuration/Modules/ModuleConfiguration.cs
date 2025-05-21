using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Providers;
using MoLibrary.Core.Module;
using MoLibrary.Tool.MoResponse;
using System.Reflection;
using Dapr.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Configuration.Modules;

public class ModuleConfiguration(ModuleConfigurationOption option) : MoModule<ModuleConfiguration, ModuleConfigurationOption>(option), IWantIterateBusinessTypes
{
    private IServiceCollection _services = null!;
    private MethodInfo _method = null!;
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Configuration;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        _services = services;
        MoConfigurationManager.Setting = Option;
        MoConfigurationManager.AppConfiguration = Option.AppConfiguration;

        services.AddOptions();
        services.AddSingleton<IMoConfigurationCardManager, MoConfigurationCardManager>();
        services.AddSingleton<IMoConfigurationServiceInfo, MoConfigurationServiceInfoDefault>();

        if (Option is { UseDaprProvider: true, AppConfiguration: ConfigurationManager manager})
        {
            Logger.LogWarning($"[MoConfiguration] Using Dapr Configuration Provider. StoreName: {Option.DaprStoreName}");
            //TODO 1.考虑使用JsonSerializer进行配置序列化存储 2.使用单例DaprClient
            var client = new DaprClientBuilder().Build();
            manager.AddDaprConfigurationStore(Option.DaprStoreName!, [], client,
                TimeSpan.FromSeconds(10));
            manager.AddStreamingDaprConfigurationStore(Option.DaprStoreName!, [], client,
                TimeSpan.FromSeconds(10));
        }


        //use reflection to call AddOptions<T> and Bind
        var method = typeof(OptionsServiceCollectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m is { Name: "AddOptions", IsGenericMethod: true }).SingleOrDefault(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceCollection);
            });

        if (method == null)
        {
            throw new InvalidOperationException("AddOptions<T> method is not found.");
        }

        _method = method;
        return Res.Ok();
    }

    public override Res PostConfigureServices(IServiceCollection services)
    {
        //巨坑：当Option的属性是List或Array等类型，有多个Configuration来源，那么这里面的元素会Append而不是替换。设计如此。dotnet/runtime #36384
        MoConfigurationManager.Setting.SetOtherSourceAction?.Invoke((ConfigurationManager) MoConfigurationManager.AppConfiguration);
        MoConfigurationCard.RefreshProviders();
        return Res.Ok();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var configType in types
                     .Where(x => x.IsClass && x.GetCustomAttribute<ConfigurationAttribute>(false) is
                     {
                         IsSubConfiguration: false
                     }))
        {
            var card = new MoConfigurationCard(configType)
            {
                FromProjectName = configType.Assembly.GetName().Name ?? "Unknown",
            };
            var provider = new LocalJsonFileProvider(card);
            provider.GenAndRegisterConfigurationFiles();
            MoConfigurationCard.Register(card);

            var configAttr = card.Configuration.Info;
            Logger.LogWarning($"AddOptions<{configType.Name}>");
            var optionBuilder = (dynamic) _method.MakeGenericMethod(configType).Invoke(null, [_services])!;

            var configAction = new Action<BinderOptions>(o =>
            {
                o.ErrorOnUnknownConfiguration =
                    configAttr.ErrorOnUnknownConfiguration ?? Option.ErrorOnUnknownConfiguration;
                o.BindNonPublicProperties = configAttr.BindNonPublicProperties ?? false;
            });
            if (configAttr.Section is { } section)
            {
                Logger.LogWarning($"Bind<{configType.Name}> to {section} (with section name)");
                MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionBuilder, Option.AppConfiguration.GetSection(section),
                    configAction);
            }
            else
            {
                Logger.LogWarning($"Bind<{configType.Name}> (without section name)");
                MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionBuilder, Option.AppConfiguration, configAction);
            }

            OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations(optionBuilder);
            yield return configType;
        }
    }

    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        return Res.Ok();
    }

    public override Res ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = option.GetSwaggerGroupName(), Description = "热配置相关内置接口" } };
            endpoints.MapGet(MoConfigurationConventions.GetConfigStatus, async (HttpResponse response,
                HttpContext context, [FromQuery] bool? onlyCurDomain,
                [FromServices] IMoConfigurationCardManager manager) =>
            {
                return Res.Create(manager.GetDomainConfigs(onlyCurDomain), ResponseCode.Ok).GetResponse();
            }).WithName("获取热配置状态信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取热配置状态信息";
                operation.Description = "获取热配置状态信息";
                operation.Tags = tagGroup;
                return operation;
            });

            endpoints.MapGet("/option/debug", async (HttpResponse response, HttpContext context) =>
                {
                    var res = new
                    {
                        debug = MoConfigurationManager.GetDebugView().Split(Environment.NewLine),
                    };

                    return res;
                }).WithName("获取DebuggingView")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取DebuggingView";
                    operation.Description = "展示配置项来源数据以及提供者";
                    operation.Tags = tagGroup;
                    return operation;
                });

            endpoints.MapGet("/option/providers", async (HttpResponse response, HttpContext context) =>
                {
                    var res = MoConfigurationManager.GetProviders();
                    return res;
                }).WithName("获取配置提供者")
                .WithOpenApi(operation =>
                {
                    operation.Summary = "获取配置提供者";
                    operation.Description = "获取配置提供者";
                    operation.Tags = tagGroup;
                    return operation;
                });
        });
        return Res.Ok();
    }

   
}