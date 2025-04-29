using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.DependencyInjection.Implements;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DependencyInjection;

public static class ModuleBuilderExtensionsAuthorization
{
    public static ModuleGuideDependencyInjection AddMoModuleDependencyInjection(this IServiceCollection services, Action<ModuleOptionDependencyInjection>? action = null)
    {
        return new ModuleGuideDependencyInjection().Register(action);
    }
}

public class ModuleDependencyInjection(ModuleOptionDependencyInjection option) : MoModule<ModuleDependencyInjection, ModuleOptionDependencyInjection>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DependencyInjection;
    }
}

public class ModuleGuideDependencyInjection : MoModuleGuide<ModuleDependencyInjection, ModuleOptionDependencyInjection, ModuleGuideDependencyInjection>
{
    public ModuleGuideDependencyInjection AddMoDependencyInjectionDefaultProvider()
    {
        ConfigureExtraServices(nameof(AddMoDependencyInjectionDefaultProvider), context =>
        {
            context.Services.AddMoDependencyInjection<DefaultConventionalRegistrar>();
        });
        return this;
    }

    public ModuleGuideDependencyInjection AddMoDependencyInjection<T>() where T : IConventionalRegistrar
    {
        ConfigureExtraServices(nameof(AddMoDependencyInjectionDefaultProvider), context =>
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var setting = context.ModuleOption;
            var registrar = ActivatorUtilities.CreateInstance<T>(context.Services.BuildServiceProvider(), setting);

            // candidates assemblies
            var candidates = Assembly.GetEntryAssembly()!.WithDomainAssemblies(setting.RelatedAssemblies);
            foreach (var candidate in candidates)
            {
                setting.Logger.LogInformation("项目自动注册Assembly：{name}", candidate.FullName);
                registrar.AddAssembly(context.Services, candidate);
            }

            context.Services.AddTransient<IMoServiceProvider, DefaultMoServiceProvider>();
            stopwatch.Stop();
            setting.Logger.LogInformation($"项目自动注册耗时：{stopwatch.ElapsedMilliseconds}ms");
        });
        return this;
    }

}