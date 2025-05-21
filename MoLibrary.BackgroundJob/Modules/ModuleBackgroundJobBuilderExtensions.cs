using Microsoft.AspNetCore.Builder;

namespace MoLibrary.BackgroundJob.Modules;

public static class ModuleBackgroundJobBuilderExtensions
{
    public static ModuleBackgroundJobGuide ConfigModuleBackgroundJob(this WebApplicationBuilder builder, Action<ModuleBackgroundJobOption>? action = null)
    {
        return new ModuleBackgroundJobGuide().Register(action);
    }
}