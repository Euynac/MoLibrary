using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Executions.State;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.JobScheduler.UI.UIJobScheduler.State;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using Monica.Modules;
using Monica.Testing.Localization;
using Monica.Testing.UI;
using Monica.UI.Shell.State;
using MudBlazor;
using MudBlazor.Services;

namespace Test.Monica.JobScheduler.UI.Infrastructure;

public sealed class JobSchedulerUiTestContext : BunitContext
{
    public const string SCOPE = "job-scheduler-ui-tests";
    public const string OWNER = "worker-a";
    public const string WORKER_REVISION = "sha256:worker-a";

    public JobSchedulerUiTestContext(
        bool isAuthorized = true,
        TimeZoneInfo? schedulerTimeZone = null,
        TimeProvider? timeProvider = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var schedulerClock = timeProvider ?? TimeProvider.System;
        Store = new InMemoryJobSchedulerStore(schedulerClock);
        SeedStore();

        var schedulerOptions = new ModuleJobSchedulerOption
        {
            SchedulerScopeKey = SCOPE,
            CatalogReleaseId = "release-1",
            DeploymentGeneration = 1,
            Role = JobSchedulerRole.ControlPlane,
            CronTimeZone = schedulerTimeZone ?? TimeZoneInfo.Utc
        };
        var facade = new JobSchedulerFacade(
            Store,
            Options.Create(schedulerOptions),
            schedulerClock,
            NullLogger<JobSchedulerFacade>.Instance);

        Services.AddMudServices();
        Services.AddSingleton<IStringLocalizer<JobSchedulerResource>, EchoStringLocalizer<JobSchedulerResource>>();
        Services.AddSingleton<IThemeState, TestThemeState>();
        Services.AddSingleton<IOptions<ModuleJobSchedulerOption>>(Options.Create(schedulerOptions));
        Services.AddSingleton<TimeProvider>(schedulerClock);
        Services.AddSingleton<IOptions<ModuleJobSchedulerUIOption>>(Options.Create(new ModuleJobSchedulerUIOption
        {
            AutoRefreshInterval = TimeSpan.Zero,
            DefaultPageSize = 20
        }));
        Access = new TestJobSchedulerUiAccess(isAuthorized);
        Services.AddSingleton<IJobSchedulerUiAccess>(Access);
        Services.AddSingleton(facade);
        Services.AddScoped<SchedulerTimePresentation>();
        Services.AddScoped<SchedulerOverviewPageStateFactory>();
        Services.AddScoped<JobCatalogPageStateFactory>();
        Services.AddScoped<JobDefinitionDetailPageStateFactory>();
        Services.AddScoped<JobExecutionsStateFactory>();
        Services.AddScoped<SchedulerStatisticsPageStateFactory>();
        _ = Render<MudPopoverProvider>();
        DialogProvider = Render<MudDialogProvider>();
    }

    public InMemoryJobSchedulerStore Store { get; }

    public IRenderedComponent<MudDialogProvider> DialogProvider { get; }

    internal TestJobSchedulerUiAccess Access { get; }

    public ActiveJobDefinition TriggeredDefinition { get; private set; } = null!;

    public ActiveJobDefinition RecurringDefinition { get; private set; } = null!;

    private void SeedStore()
    {
        var manifest = new JobCatalogReleaseManifest(
            SCOPE,
            "release-1",
            [new JobCatalogOwnerManifest(OWNER, WORKER_REVISION)]);
        Store.StageReleaseAsync(new JobCatalogReleaseStage(manifest, 1)).GetAwaiter().GetResult();
        Store.PublishOwnerSnapshotAsync(new JobOwnerCatalogSnapshot(
            SCOPE,
            "release-1",
            OWNER,
            WORKER_REVISION,
            [
                new JobDeclaration
                {
                    JobKey = "Sample.Jobs.RecurringCleanup",
                    JobName = "Recurring cleanup",
                    JobType = JobType.Recurring,
                    CronExpression = "0 */5 * * * *",
                    TimeZoneId = "UTC",
                    MaxConcurrency = 1
                },
                new JobDeclaration
                {
                    JobKey = "Sample.Jobs.GenerateReport",
                    JobArgsKey = "Sample.Jobs.GenerateReportArgs",
                    JobName = "Generate report",
                    JobType = JobType.Triggered,
                    MaxConcurrency = 2
                }
            ])).GetAwaiter().GetResult();
        Store.TryActivateReleaseAsync(SCOPE, "release-1").GetAwaiter().GetResult();
        var activeCatalog = Store.GetActiveCatalogAsync(SCOPE).GetAwaiter().GetResult()!;
        TriggeredDefinition = activeCatalog.Definitions.Single(definition =>
            definition.Declaration.JobType == JobType.Triggered);
        RecurringDefinition = activeCatalog.Definitions.Single(definition =>
            definition.Declaration.JobType == JobType.Recurring);
        Store.SynchronizeRecurringScheduleAsync(new RecurringScheduleSynchronization
        {
            Template = RecurringDefinition.CreateExecutionTemplate(),
            Schedule = new RecurringScheduleDefinition
            {
                CronExpression = RecurringDefinition.Declaration.CronExpression!,
                TimeZoneId = RecurringDefinition.Declaration.TimeZoneId!,
                StartTimeUtc = RecurringDefinition.Declaration.StartTimeUtc,
                EndTimeUtc = RecurringDefinition.Declaration.EndTimeUtc
            },
            ChangeEpoch = activeCatalog.Version.ChangeEpoch,
            SuspensionReasons = JobRecurringScheduleSuspensionReason.None
        }).GetAwaiter().GetResult();
        Store.RegisterWorkerCapabilityAsync(
            new WorkerCapabilityRegistration
            {
                SchedulerScopeKey = SCOPE,
                OwnerKey = OWNER,
                WorkerRevisionId = WORKER_REVISION,
                WorkerInstanceId = "worker-a-pod-1",
                JobRevisionIds = activeCatalog.Definitions.Select(definition => definition.JobRevisionId).ToArray()
            },
            TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();
    }

    internal sealed class TestJobSchedulerUiAccess(bool isAuthorized) : IJobSchedulerUiAccess
    {
        private TaskCompletionSource<bool>? _authorizationGate;
        private TaskCompletionSource<bool>? _authorizationStarted;

        internal bool IsAuthorized { get; set; } = isAuthorized;

        internal Exception? AuthorizationException { get; set; }

        internal Task AuthorizationStarted => _authorizationStarted?.Task ?? Task.CompletedTask;

        internal void BlockAuthorization()
        {
            _authorizationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _authorizationGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _authorizationStarted?.TrySetResult(true);
            if (_authorizationGate is not null)
            {
                await _authorizationGate.Task.WaitAsync(cancellationToken);
            }

            if (AuthorizationException is not null)
            {
                throw AuthorizationException;
            }

            return IsAuthorized;
        }
    }
}
