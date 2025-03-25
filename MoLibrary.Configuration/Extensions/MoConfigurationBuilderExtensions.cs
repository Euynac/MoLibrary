using System.Reflection;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Providers;
using MoLibrary.Core.Extensions;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Extensions;

public static class MoConfigurationBuilderExtensions
{
    /// <summary>
    /// 根据项目获取领域信息，用于完善微服务配置状态接口信息返回
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void AddMoConfigurationDomainInfo<T>(this IServiceCollection services) where T :class, IMoConfigurationServiceInfo
    {
        services.Replace(ServiceDescriptor.Singleton<IMoConfigurationServiceInfo, T>());
    }

    /// <summary>
    /// 注册MoConfiguration
    /// </summary>
    /// <param name="services"></param>
    /// <param name="appConfiguration"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddMoConfiguration(this IServiceCollection services,
        IConfiguration appConfiguration, Action<MoConfigurationSetting>? action = null)
    {
        var setting = new MoConfigurationSetting();
        action?.Invoke(setting);
        MoConfigurationManager.Setting = setting;
        MoConfigurationManager.AppConfiguration = appConfiguration;
        var log = MoConfigurationManager.Logger;

        services.AddOptions();
        services.AddSingleton<IMoConfigurationCardManager, MoConfigurationCardManager>();
        services.AddSingleton<IMoConfigurationServiceInfo, MoConfigurationServiceInfoDefault>();

        // candidates assemblies
        var assemblies = new List<Assembly>();
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null) assemblies.Add(entryAssembly);
        if (setting.ConfigurationAssemblyLocation is { } name)
        {
            var referredAssemblies = entryAssembly?.GetReferencedAssemblies().ToList();
            if (referredAssemblies != null)
            {
                assemblies.AddRange(
                    from assemblyName in referredAssemblies
                    where name.Any(x => assemblyName.Name.Contains(x))
                    select Assembly.Load(assemblyName));
            }
        }

        if (assemblies.Count == 0)
        {
            log.LogWarning(
                $"During MoConfiguration Register, No assembly found.{setting.ConfigurationAssemblyLocation?.StringJoin(",").BeAfter("The filter method is ")}");
            return services;
        }

        if (setting.UseDaprProvider && appConfiguration is ConfigurationManager manager)
        {
            log.LogWarning($"[MoConfiguration] Using Dapr Configuration Provider. StoreName: {setting.DaprStoreName}");
            //TODO 1.考虑使用JsonSerializer进行配置序列化存储 2.使用单例DaprClient
            var client = new DaprClientBuilder().Build();
            manager.AddDaprConfigurationStore(setting.DaprStoreName!, [], client,
                TimeSpan.FromSeconds(10));
            manager.AddStreamingDaprConfigurationStore(setting.DaprStoreName!, [], client,
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


        if (!setting.EnableConfigRegisterLogging)
        {
            log = null;
        }

        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            //log assembly
            log?.LogDebug($"Adding configuration from assembly: {assemblyName}");
            foreach (var configType in assembly.GetTypes()
                         .Where(x => x.IsClass && x.GetCustomAttribute<ConfigurationAttribute>(false) is
                         {
                             IsSubConfiguration: false
                         }))
            {
                var card = new MoConfigurationCard(configType)
                {
                    FromProjectName = assemblyName ?? "Unknown",
                };
                var provider = new LocalJsonFileProvider(card);
                provider.GenAndRegisterConfigurationFiles();
                MoConfigurationCard.Register(card);

                var configAttr = card.Configuration.Info;
                log?.LogWarning($"AddOptions<{configType.Name}>");
                var optionBuilder = (dynamic)method.MakeGenericMethod(configType).Invoke(null, [services])!;

                var configAction = new Action<BinderOptions>(o =>
                {
                    o.ErrorOnUnknownConfiguration =
                        configAttr.ErrorOnUnknownConfiguration ?? setting.ErrorOnUnknownConfiguration;
                    o.BindNonPublicProperties = configAttr.BindNonPublicProperties ?? false;
                });
                if (configAttr.Section is { } section)
                {
                    log?.LogWarning($"Bind<{configType.Name}> to {section} (with section name)");
                    MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionBuilder, appConfiguration.GetSection(section),
                        configAction);
                }
                else
                {
                    log?.LogWarning($"Bind<{configType.Name}> (without section name)");
                    MoExtendedOptionsBuilderConfigurationExtensions.Bind(optionBuilder, appConfiguration, configAction);
                }

                OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations(optionBuilder);
            }
        }
        //巨坑：当Option的属性是List或Array等类型，有多个Configuration来源，那么这里面的元素会Append而不是替换。设计如此。dotnet/runtime #36384
        MoConfigurationManager.Setting.SetOtherSourceAction?.Invoke((ConfigurationManager)MoConfigurationManager.AppConfiguration);
        MoConfigurationCard.RefreshProviders();

        return services;
    }


   
    /// <summary>
    /// 配置MoConfiguration Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoConfiguration(this IApplicationBuilder app, string? groupName = "MoConfiguration")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "热配置相关内置接口" } };
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
                        debug=MoConfigurationManager.GetDebugView().Split(Environment.NewLine),
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
    }
}