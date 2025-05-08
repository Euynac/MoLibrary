using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.DependencyInjection.Implements;
using MoLibrary.DependencyInjection.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DependencyInjection;

public static class MoDependencyInjectionExtensions
{
    public static IServiceCollection AddMoDependencyInjectionDefaultProvider(this IServiceCollection services, Action<ModuleDependencyInjectionOption>? action = null)
    {
        return services.AddMoDependencyInjection<DefaultConventionalRegistrar>(action);
    }

    public static IServiceCollection AddMoDependencyInjection<T>(this IServiceCollection services,
        Action<ModuleDependencyInjectionOption>? action = null) where T : IConventionalRegistrar
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var setting = new ModuleDependencyInjectionOption();
        action?.Invoke(setting);
        var registrar = ActivatorUtilities.CreateInstance<T>(services.BuildServiceProvider(), setting);

        // candidates assemblies
        var candidates = Assembly.GetEntryAssembly()!.WithDomainAssemblies(setting.RelatedAssemblies);
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