using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.BackgroundJob.Modules;

public static class ModuleBackgroundJobBuilderExtensions
{
    public static ModuleBackgroundJobGuide AddMoModuleBackgroundJob(this IServiceCollection services, Action<ModuleBackgroundJobOption>? action = null)
    {
        return new ModuleBackgroundJobGuide().Register(action);
    }
}