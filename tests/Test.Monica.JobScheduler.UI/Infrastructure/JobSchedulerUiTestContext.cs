using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.Modules;
using Monica.UI.Shell.State;
using MudBlazor.Services;
using NSubstitute;
using Monica.UnitTests.Localization;
using Monica.UnitTests.UI;

namespace Test.Monica.JobScheduler.UI.Infrastructure;

public sealed class JobSchedulerUiTestContext : BunitContext
{
    public JobSchedulerUiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddMudServices();
        Services.AddSingleton<IStringLocalizer<JobSchedulerResource>, EchoStringLocalizer<JobSchedulerResource>>();
        Services.AddSingleton<IThemeState, TestThemeState>();
        Services.AddSingleton<IOptions<ModuleJobSchedulerOption>>(Options.Create(new ModuleJobSchedulerOption()));
        Services.AddSingleton<IOptions<ModuleJobSchedulerUIOption>>(Options.Create(new ModuleJobSchedulerUIOption()));
        Services.AddSingleton<JobStateColorResolver>();
        Services.AddSingleton<JobArgsJsonSchemaSupport>();
        Services.AddSingleton<CronExpressionSupport>();
    }

    public void AddDashboardFacade(JobSchedulerDashboardFacade facade)
    {
        Services.AddSingleton(facade);
    }

    public static JobSchedulerDashboardFacade CreateFailingDashboardFacade(string exceptionMessage)
    {
        var cacheService = Substitute.For<IJobDefinitionCacheService>();
        cacheService.GetAllDefinitionsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IReadOnlyList<global::Monica.JobScheduler.Models.JobDefinition>>(
                new InvalidOperationException(exceptionMessage)));

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddHealthChecks()
            .AddCheck("JobScheduler", () => HealthCheckResult.Healthy("ok"))
            .Services
            .BuildServiceProvider();

        return new JobSchedulerDashboardFacade(
            cacheService,
            Substitute.For<IJobMetadataRepository>(),
            Substitute.For<IJobConcurrencyGuard>(),
            provider.GetRequiredService<HealthCheckService>(),
            NullLogger<JobSchedulerDashboardFacade>.Instance);
    }
}
