using Microsoft.AspNetCore.Builder;

namespace MoLibrary.JobScheduler.Modules;

public static class ModuleJobSchedulerBuilderExtensions
{
    /// <summary>
    /// Configures the Job Scheduler module for the application.
    /// </summary>
    public static ModuleJobSchedulerGuide ConfigModuleJobScheduler(
        this WebApplicationBuilder builder,
        Action<ModuleJobSchedulerOption>? action = null)
    {
        return new ModuleJobSchedulerGuide().Register(action);
    }
}
