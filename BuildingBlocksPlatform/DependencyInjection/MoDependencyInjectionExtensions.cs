using System.Diagnostics;
using System.Reflection;
using BuildingBlocksPlatform.DependencyInjection.CoreInterfaces;
using BuildingBlocksPlatform.DependencyInjection.Implements;
using BuildingBlocksPlatform.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.DependencyInjection;

public static class MoDependencyInjectionExtensions
{
    public static IServiceCollection AddMoDependencyInjectionDefaultProvider(this IServiceCollection services, Action<MoDependencyOption>? action = null)
    {
        return AddMoDependencyInjection<DefaultConventionalRegistrar>(services, action);
    }

    public static IServiceCollection AddMoDependencyInjection<T>(this IServiceCollection services,
        Action<MoDependencyOption>? action = null) where T : IConventionalRegistrar
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var setting = new MoDependencyOption();
        action?.Invoke(setting);
        var registrar = ActivatorUtilities.CreateInstance<T>(services.BuildServiceProvider(), setting);

        // candidates assemblies
        var candidates = Assembly.GetEntryAssembly()!.GetRelatedAssemblies(setting.RelatedAssemblies);
        foreach (var candidate in candidates)
        {
            setting.Logger.LogInformation("项目自动注册Assembly：{name}", candidate.FullName);
            registrar.AddAssembly(services, candidate);
        }

        services.AddTransient<IMoServiceProvider, DefaultMoServiceProvider>();
        stopwatch.Stop();
        setting.Logger.LogInformation($"项目自动注册耗时：{stopwatch.ElapsedMilliseconds}ms");


        return services;
    }
}