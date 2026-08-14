using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
using Monica.HealthCheck.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.JobScheduler.Utils;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJobSchedulerBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Adds the durable JobScheduler runtime. A store, scope, and immutable catalog release must be selected on
        /// the returned registration before the module graph is finalized.
        /// </summary>
        public ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> AddJobScheduler(
            Action<ModuleJobSchedulerOption>? action = null)
        {
            return builder.AddModule<ModuleJobScheduler, ModuleJobSchedulerOption>(action);
        }
    }
}

/// <summary>
/// Composes the immutable catalog control plane and revision-aware durable worker plane.
/// </summary>
public sealed class ModuleJobScheduler : MonicaModule<ModuleJobSchedulerOption>
{
    internal const string STORE_FEATURE = "scheduler-store";
    internal const string SCOPE_FEATURE = "scheduler-scope";
    internal const string RELEASE_FEATURE = "catalog-release";

    private readonly List<LocalJobDefinition> _localJobDefinitions = [];

    /// <inheritdoc />
    public override void ValidateOptions(ModuleJobSchedulerOption options, string? profileName)
    {
        ValidatePositive(options.ControlPlanePollInterval, nameof(options.ControlPlanePollInterval));
        ValidatePositive(options.WorkerPollInterval, nameof(options.WorkerPollInterval));
        ValidatePositive(options.WorkerCapabilityLeaseDuration, nameof(options.WorkerCapabilityLeaseDuration));
        ValidatePositive(options.WorkerCapabilityRenewInterval, nameof(options.WorkerCapabilityRenewInterval));
        ValidatePositive(options.ExecutionLeaseDuration, nameof(options.ExecutionLeaseDuration));
        ValidatePositive(options.ExecutionLeaseRenewInterval, nameof(options.ExecutionLeaseRenewInterval));
        ValidatePositive(options.ExecutionRetryDelay, nameof(options.ExecutionRetryDelay));
        ValidatePositive(options.ExecutionCancellationGracePeriod, nameof(options.ExecutionCancellationGracePeriod));
        ValidatePositive(options.WorkerShutdownGracePeriod, nameof(options.WorkerShutdownGracePeriod));
        ValidatePositive(options.HistoryCleanupInterval, nameof(options.HistoryCleanupInterval));

        if (options.WorkerCapabilityRenewInterval >= options.WorkerCapabilityLeaseDuration)
        {
            throw new InvalidOperationException(
                $"{nameof(options.WorkerCapabilityRenewInterval)} must be shorter than " +
                $"{nameof(options.WorkerCapabilityLeaseDuration)}.");
        }

        if (options.ExecutionLeaseRenewInterval >= options.ExecutionLeaseDuration)
        {
            throw new InvalidOperationException(
                $"{nameof(options.ExecutionLeaseRenewInterval)} must be shorter than " +
                $"{nameof(options.ExecutionLeaseDuration)}.");
        }

        if (options.MaxWorkerExecutionThreads < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxWorkerExecutionThreads)} must be greater than zero.");
        }

        if (options.MaxClaimBatchSize is < 1 or > JobClaimRequest.MAX_COUNT)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxClaimBatchSize)} must be between 1 and {JobClaimRequest.MAX_COUNT}.");
        }

        if (options.MaxRecurringMaterializationsPerCycle < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxRecurringMaterializationsPerCycle)} must be greater than zero.");
        }

        if (options.MaxExpiredLeaseRecoveriesPerCycle < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxExpiredLeaseRecoveriesPerCycle)} must be greater than zero.");
        }

        if (options.MaxExecutionHistoryEntriesPerExecution < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxExecutionHistoryEntriesPerExecution)} must be greater than zero.");
        }

        if (options.MaxExecutionHistoryMessageLength < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxExecutionHistoryMessageLength)} must be greater than zero.");
        }

        if (options.MaxHistoryDeletionsPerCycle < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxHistoryDeletionsPerCycle)} must be greater than zero.");
        }

        if (options.MaxRetainedOrphanedExecutions < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(options.MaxRetainedOrphanedExecutions)} cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(options.SchedulerScopeKey))
        {
            throw new InvalidOperationException("A scheduler scope must be configured with UseSchedulerScope(...).");
        }

        if (string.IsNullOrWhiteSpace(options.CatalogReleaseId))
        {
            throw new InvalidOperationException(
                "An immutable catalog release must be configured with UseCatalogRelease(...).");
        }

        if (!Enum.IsDefined(options.Role))
        {
            throw new InvalidOperationException($"Unsupported scheduler role '{options.Role}'.");
        }

        if (options.DeploymentGeneration < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(options.DeploymentGeneration)} must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerInstanceId))
        {
            throw new InvalidOperationException($"{nameof(options.WorkerInstanceId)} cannot be empty.");
        }

        JobSchedulerIdentity.ValidateStandard(options.SchedulerScopeKey, nameof(options.SchedulerScopeKey));
        JobSchedulerIdentity.ValidateStandard(options.CatalogReleaseId, nameof(options.CatalogReleaseId));
        JobSchedulerIdentity.ValidateStandard(options.WorkerInstanceId, nameof(options.WorkerInstanceId));
        foreach (var owner in options.CatalogOwners)
        {
            ArgumentNullException.ThrowIfNull(owner);
            JobSchedulerIdentity.ValidateStandard(owner.OwnerId, nameof(owner.OwnerId));
            JobSchedulerIdentity.ValidateStandard(owner.WorkerRevisionId, nameof(owner.WorkerRevisionId));
        }

        var duplicateOwner = options.CatalogOwners
            .GroupBy(static owner => owner.OwnerId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateOwner is not null)
        {
            throw new InvalidOperationException(
                $"Catalog owner '{duplicateOwner.Key}' appears more than once in the release manifest.");
        }

        if (options.LocalOwnerId is not null)
        {
            JobSchedulerIdentity.ValidateStandard(options.LocalOwnerId, nameof(options.LocalOwnerId));
        }

        if (options.LocalWorkerRevisionId is not null)
        {
            JobSchedulerIdentity.ValidateStandard(
                options.LocalWorkerRevisionId,
                nameof(options.LocalWorkerRevisionId));
        }

        if (options.Role is JobSchedulerRole.Worker or JobSchedulerRole.Standalone)
        {
            var localOwnerId = options.LocalOwnerId ?? options.GetProjectName();
            JobSchedulerIdentity.ValidateStandard(localOwnerId, nameof(options.LocalOwnerId));
            var manifestOwner = options.CatalogOwners.SingleOrDefault(owner => string.Equals(
                owner.OwnerId,
                localOwnerId,
                StringComparison.Ordinal));
            if (manifestOwner is null)
            {
                throw new InvalidOperationException(
                    $"Catalog owner manifest does not contain local worker owner '{localOwnerId}'.");
            }

            if (options.LocalWorkerRevisionId is { } localRevision
                && !string.Equals(localRevision, manifestOwner.WorkerRevisionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Local worker revision '{localRevision}' does not match manifest revision " +
                    $"'{manifestOwner.WorkerRevisionId}' for owner '{localOwnerId}'.");
            }
        }
    }

    /// <summary>
    /// Discovers executable job types once and captures their immutable code declarations.
    /// </summary>
    public override void DeclareTypeDiscovery(TypeDiscoveryPlan<ModuleJobSchedulerOption> discovery)
    {
        var recurringJobs = TypeQuery.ConcreteClass.AssignableTo<IRecurringJob>();
        var triggeredJobs = TypeQuery.ConcreteClass.ImplementsOpenGeneric(typeof(ITriggeredJob<>));

        discovery.Match(
            TypeQuery.AnyOf(recurringJobs, triggeredJobs),
            (context, matches) =>
            {
                foreach (var match in matches)
                {
                    var isRecurring = match.Shape.IsAssignableTo(typeof(IRecurringJob));
                    var argumentsType = isRecurring
                        ? null
                        : match.OpenGenericInterfaces.Single().GenericArguments[0];
                    var localDefinition = CreateLocalDefinition(
                        match,
                        isRecurring ? JobType.Recurring : JobType.Triggered,
                        argumentsType);

                    _localJobDefinitions.Add(localDefinition);
                    context.Registrations.Add(ServiceDescriptor.Transient(
                        localDefinition.JobClrType,
                        localDefinition.JobClrType));
                }
            });
    }

    /// <inheritdoc />
    public override void PostConfigureServices(ModuleContext<ModuleJobSchedulerOption> context)
    {
        var services = context.Services;
        var runsControlPlane = Option.Role is JobSchedulerRole.ControlPlane or JobSchedulerRole.Standalone;
        var runsWorkerPlane = Option.Role is JobSchedulerRole.Worker or JobSchedulerRole.Standalone;
        IReadOnlyList<LocalJobDefinition> localDefinitions =
            Array.AsReadOnly(_localJobDefinitions.OrderBy(
                static definition => definition.Declaration.JobKey,
                StringComparer.Ordinal).ToArray());

        if (Option.Role == JobSchedulerRole.ControlPlane && localDefinitions.Count != 0)
        {
            throw new InvalidOperationException(
                $"A {nameof(JobSchedulerRole.ControlPlane)} scheduler host cannot own executable job types. " +
                $"Use {nameof(JobSchedulerRole.Worker)} or {nameof(JobSchedulerRole.Standalone)} for jobs.");
        }

        var hostIdentity = JobSchedulerHostIdentity.Create(Option);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(hostIdentity);
        services.AddSingleton<JobSchedulerRuntimeState>();
        services.AddSingleton(localDefinitions);

        if (runsWorkerPlane)
        {
            services.AddSingleton<JobRegistry>();
            services.AddSingleton<JobExecutor>();
            services.AddSingleton<JobOrchestrator>();
            services.AddSingleton<ITriggeredJobManager, TriggeredJobManager>();
            services.AddSingleton<JobWorkerHostedService>();
            services.AddHostedService(static provider => provider.GetRequiredService<JobWorkerHostedService>());
        }

        if (runsControlPlane)
        {
            services.AddSingleton<JobSchedulerFacade>();
            services.AddSingleton<JobControlPlaneHostedService>();
            services.AddHostedService(static provider => provider.GetRequiredService<JobControlPlaneHostedService>());
        }

        services.AddHealthChecks().AddMonicaReadinessCheck<JobSchedulerHealthCheck>(
            "monica.job-scheduler",
            tags: ["scheduler"]);
    }

    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleHostedService, ModuleHostedServiceOption>();
        module.Require<ModuleHealthCheck, ModuleHealthCheckOption>();
        module.RequireFeature(STORE_FEATURE);
        module.RequireFeature(SCOPE_FEATURE);
        module.RequireFeature(RELEASE_FEATURE);
    }

    /// <inheritdoc />
    public override void DeclareContracts(ModuleContractDescriptor<ModuleJobSchedulerOption> contracts)
    {
        contracts.RequireService<IJobSchedulerStore>();
    }

    private LocalJobDefinition CreateLocalDefinition(
        BusinessTypeMatch match,
        JobType jobType,
        Type? argumentsType)
    {
        var jobClrType = match.Type;
        var attribute = match.Shape.GetAttribute<JobConfigAttribute>();
        var jobKey = jobClrType.FullName
                     ?? throw new JobRegistrationException(
                         $"Job type '{jobClrType.Name}' must have a full name.",
                         jobClrType.Name);
        var argumentsKey = argumentsType?.FullName;
        if (argumentsType is not null && argumentsKey is null)
        {
            throw new JobRegistrationException(
                $"Argument type '{argumentsType.Name}' for job '{jobKey}' must have a full name.",
                jobKey);
        }

        var declaration = new JobDeclaration
        {
            JobKey = jobKey,
            JobArgsKey = argumentsKey,
            JobName = attribute?.JobName ?? jobClrType.Name,
            Description = attribute?.Description,
            JobType = jobType,
            MaxConcurrency = attribute?.MaxConcurrencyBridge ?? 1,
            RetryCount = attribute?.RetryCountBridge ?? 0,
            MaxExecutionTimeout = attribute?.MaxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabledByDefault = attribute?.IsDisabledBridge ?? false,
            CronExpression = jobType == JobType.Recurring ? attribute?.CronSchedule : null,
            TimeZoneId = jobType == JobType.Recurring ? Option.CronTimeZone.Id : null,
            StartTimeUtc = jobType == JobType.Recurring ? NormalizeBoundary(attribute?.StartTimeBridge) : null,
            EndTimeUtc = jobType == JobType.Recurring ? NormalizeBoundary(attribute?.EndTimeBridge) : null
        };

        ValidateDeclaration(declaration);
        return new LocalJobDefinition
        {
            Declaration = declaration,
            JobClrType = jobClrType,
            JobArgsClrType = argumentsType
        };
    }

    private void ValidateDeclaration(JobDeclaration declaration)
    {
        JobSchedulerIdentity.ValidateJobKey(declaration.JobKey, nameof(declaration.JobKey));
        JobSchedulerIdentity.ValidateStandard(declaration.JobName, nameof(declaration.JobName));
        if (declaration.JobArgsKey is not null)
        {
            JobSchedulerIdentity.ValidateStandard(declaration.JobArgsKey, nameof(declaration.JobArgsKey));
        }

        if (declaration.MaxConcurrency < 1)
        {
            throw new JobRegistrationException(
                $"Job '{declaration.JobKey}' must allow at least one concurrent execution.",
                declaration.JobKey);
        }

        if (declaration.RetryCount < 0)
        {
            throw new JobRegistrationException(
                $"Job '{declaration.JobKey}' cannot configure a negative retry count.",
                declaration.JobKey);
        }

        if (declaration.MaxExecutionTimeout <= TimeSpan.Zero)
        {
            throw new JobRegistrationException(
                $"Job '{declaration.JobKey}' must configure a positive execution timeout.",
                declaration.JobKey);
        }

        if (declaration.JobType != JobType.Recurring)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(declaration.CronExpression))
        {
            throw new JobRegistrationException(
                $"Recurring job '{declaration.JobKey}' must declare a cron schedule.",
                declaration.JobKey);
        }

        if (string.IsNullOrWhiteSpace(declaration.TimeZoneId))
        {
            throw new JobRegistrationException(
                $"Recurring job '{declaration.JobKey}' must capture a cron timezone.",
                declaration.JobKey);
        }

        try
        {
            _ = CronHelper.Parse(declaration.CronExpression);
            _ = TimeZoneInfo.FindSystemTimeZoneById(declaration.TimeZoneId);
        }
        catch (Exception exception)
        {
            throw new JobRegistrationException(
                $"Recurring job '{declaration.JobKey}' has invalid cron schedule " +
                $"'{declaration.CronExpression}': {exception.Message}",
                declaration.JobKey);
        }

        if (declaration.StartTimeUtc is { } start && declaration.EndTimeUtc is { } end && end < start)
        {
            throw new JobRegistrationException(
                $"Recurring job '{declaration.JobKey}' ends before it starts.",
                declaration.JobKey);
        }
    }

    private DateTimeOffset? NormalizeBoundary(DateTime? boundary)
    {
        if (boundary is not { } value)
        {
            return null;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
            _ => new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(value, Option.CronTimeZone),
                TimeSpan.Zero)
        };
    }

    private static void ValidatePositive(TimeSpan value, string propertyName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{propertyName} must be greater than zero.");
        }
    }
}

/// <summary>
/// Adds persistence and deployment identity to a JobScheduler module registration.
/// </summary>
public static class ModuleJobSchedulerRegistrationExtensions
{
    /// <summary>
    /// Registers a custom unified scheduler store. The implementation must make catalog, activation, queue, lease,
    /// capability, and recurring-cursor mutations durable and atomic according to <see cref="IJobSchedulerStore"/>.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseStore<TStore>(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration)
        where TStore : class, IJobSchedulerStore
    {
        return registration
            .PostConfigureServices(static context =>
                context.Services.TryAddSingleton<IJobSchedulerStore, TStore>())
            .SatisfyFeature(ModuleJobScheduler.STORE_FEATURE);
    }

    /// <summary>
    /// Uses a process-local scheduler store. Select this provider for standalone development and deterministic tests;
    /// it does not survive process restarts and cannot coordinate multiple hosts.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseInMemoryStore(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration)
    {
        return registration.UseStore<InMemoryJobSchedulerStore>();
    }

    /// <summary>
    /// Configures the stable scheduler namespace shared by every control-plane and worker host in one environment.
    /// Different environments or logical scheduler installations must use different scope keys.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseSchedulerScope(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration,
        string scopeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        return registration
            .Configure(options => options.SchedulerScopeKey = scopeKey)
            .SatisfyFeature(ModuleJobScheduler.SCOPE_FEATURE);
    }

    /// <summary>
    /// Configures one immutable catalog release and its complete owner-to-worker-revision manifest. Every host
    /// participating in the same release must provide byte-equivalent values. Omitting an owner intentionally removes
    /// that owner's jobs when this release activates. An empty manifest is valid for a control-plane host and removes
    /// every owner; worker and standalone hosts must include their local owner.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseCatalogRelease(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration,
        string releaseId,
        long deploymentGeneration,
        IEnumerable<JobCatalogOwnerManifest> owners)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseId);
        if (deploymentGeneration < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deploymentGeneration),
                deploymentGeneration,
                "Deployment generation must be greater than zero.");
        }
        ArgumentNullException.ThrowIfNull(owners);
        var ownerManifest = owners.ToArray();

        return registration
            .Configure(options =>
            {
                options.CatalogReleaseId = releaseId;
                options.DeploymentGeneration = deploymentGeneration;
                options.CatalogOwners.Clear();
                options.CatalogOwners.AddRange(ownerManifest);
            })
            .SatisfyFeature(ModuleJobScheduler.RELEASE_FEATURE);
    }

    /// <summary>
    /// Configures this host as a replicated scheduler control plane. It stages and activates releases and materializes
    /// recurring work, but does not publish or execute a worker catalog.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> AsControlPlane(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration) =>
        registration.Configure(static options => options.Role = JobSchedulerRole.ControlPlane);

    /// <summary>
    /// Configures this host as an executable worker. It publishes one catalog owner and claims only exact-revision
    /// work; it does not materialize recurring schedules.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> AsWorker(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration) =>
        registration.Configure(static options => options.Role = JobSchedulerRole.Worker);

    /// <summary>
    /// Configures one process to own both scheduler control-plane and worker responsibilities.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> AsStandalone(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration) =>
        registration.Configure(static options => options.Role = JobSchedulerRole.Standalone);

    /// <summary>
    /// Overrides the local worker owner identity. By default the worker uses the Monica project name and resolves its
    /// executable revision from the matching release-manifest entry. Configure this only when deployment ownership
    /// intentionally differs from the project identity.
    /// </summary>
    public static ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> UseLocalWorkerIdentity(
        this ModuleRegistration<ModuleJobScheduler, ModuleJobSchedulerOption> registration,
        string ownerId,
        string workerRevisionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRevisionId);
        return registration.Configure(options =>
        {
            options.LocalOwnerId = ownerId;
            options.LocalWorkerRevisionId = workerRevisionId;
        });
    }
}

/// <summary>
/// Configures the immutable catalog, leased queue, worker, and maintenance behavior of JobScheduler.
/// </summary>
public sealed class ModuleJobSchedulerOption : ModuleOptions<ModuleJobScheduler>
{
    /// <summary>
    /// Gets or sets the timezone used to evaluate recurring cron expressions. All persisted occurrences are converted
    /// to UTC. The default is the operating system's local timezone.
    /// </summary>
    public TimeZoneInfo CronTimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>
    /// Gets the stable scheduler namespace configured through <see cref="ModuleJobSchedulerRegistrationExtensions.UseSchedulerScope"/>.
    /// </summary>
    public string SchedulerScopeKey { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Monica project name used as the default local catalog owner identity.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Gets the immutable deployment release identifier configured by the deployment composition.
    /// </summary>
    public string CatalogReleaseId { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the deployment orchestrator's monotonic generation used to order desired-release intent.
    /// </summary>
    public long DeploymentGeneration { get; internal set; }

    /// <summary>
    /// Gets the complete expected owner manifest for <see cref="CatalogReleaseId"/>.
    /// </summary>
    public List<JobCatalogOwnerManifest> CatalogOwners { get; } = [];

    /// <summary>
    /// Gets the optional local owner override. Worker hosts otherwise use <see cref="GetProjectName"/>.
    /// </summary>
    public string? LocalOwnerId { get; internal set; }

    /// <summary>
    /// Gets the optional local executable-revision override. Worker hosts otherwise use the revision declared for
    /// their resolved owner in <see cref="CatalogOwners"/>.
    /// </summary>
    public string? LocalWorkerRevisionId { get; internal set; }

    /// <summary>
    /// Gets or sets this host's scheduler responsibility independently of service discovery. The default is Worker.
    /// </summary>
    public JobSchedulerRole Role { get; set; } = JobSchedulerRole.Worker;

    /// <summary>
    /// Gets or sets the concrete process identity used to fence capability and execution leases. The default uses the
    /// Kubernetes hostname when present, otherwise the machine name and process identifier.
    /// </summary>
    public string WorkerInstanceId { get; set; } =
        Environment.GetEnvironmentVariable("HOSTNAME") ?? $"{Environment.MachineName}:{Environment.ProcessId}";

    /// <summary>
    /// Gets or sets whether automatic recurring occurrence materialization is suppressed with an explicit debug-mode
    /// reason. This setting does not change operator policy or prevent an explicit recurring run-now command. Triggered
    /// work is unaffected. The default is <see langword="false"/>.
    /// </summary>
    public bool RecurringJobDebugMode { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of attempts this process executes concurrently. The default is 4.
    /// </summary>
    public int MaxWorkerExecutionThreads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum durable rows claimed in one worker poll. The effective value never exceeds the
    /// currently available worker slots and must be between 1 and 256. The default is 16.
    /// </summary>
    public int MaxClaimBatchSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets how often each control-plane replica checks catalog activation, recurring cursors, and expired
    /// leases. Store-level compare-and-swap operations coordinate replicas. The default is one second.
    /// </summary>
    public TimeSpan ControlPlanePollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets how often a worker polls the durable queue when it has execution capacity. The default is 250 ms.
    /// </summary>
    public TimeSpan WorkerPollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets the worker-capability lease duration. It must exceed <see cref="WorkerCapabilityRenewInterval"/>.
    /// The default is 30 seconds.
    /// </summary>
    public TimeSpan WorkerCapabilityLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the monotonic interval between worker capability renewals. The interval is measured locally from
    /// the last successful registration or renewal and does not compare the worker clock with the store clock. The
    /// default is 10 seconds.
    /// </summary>
    public TimeSpan WorkerCapabilityRenewInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the duration of each fenced execution lease. It must exceed
    /// <see cref="ExecutionLeaseRenewInterval"/>. The default is 30 seconds.
    /// </summary>
    public TimeSpan ExecutionLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how often a running attempt renews its execution lease and observes durable cancellation. The
    /// default is 10 seconds.
    /// </summary>
    public TimeSpan ExecutionLeaseRenewInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the delay applied by the durable store before a failed attempt becomes retryable. The default is
    /// five seconds.
    /// </summary>
    public TimeSpan ExecutionRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long a worker keeps its lease while waiting for job code to observe a timeout, durable
    /// cancellation, lease loss, or execution-loop failure. If the job still has not exited, the worker requests host
    /// shutdown so an external supervisor can terminate the unsafe process before the lease is recovered elsewhere.
    /// The default is 15 seconds.
    /// </summary>
    public TimeSpan ExecutionCancellationGracePeriod { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the maximum total history entries retained for one execution, including state transitions,
    /// job-code logs, and cancellation audit entries. Each append atomically evicts the oldest entries beyond this
    /// boundary while authoritative execution state remains on the execution record. The default is 1,000.
    /// </summary>
    public int MaxExecutionHistoryEntriesPerExecution { get; set; } =
        JobExecutionHistoryLimits.DEFAULT_MAX_ENTRIES;

    /// <summary>
    /// Gets or sets the maximum number of characters persisted for every execution-history message, including job
    /// logs, failure details, state transitions, and cancellation reasons. Longer messages are truncated with an
    /// ellipsis. The default is 4,096 characters.
    /// </summary>
    public int MaxExecutionHistoryMessageLength { get; set; } =
        JobExecutionHistoryLimits.DEFAULT_MAX_MESSAGE_LENGTH;

    /// <summary>
    /// Gets or sets how long graceful shutdown keeps capability and execution leases alive while cooperative jobs
    /// exit. After this boundary, surviving leases expire and are recovered by the control plane. The default is 15
    /// seconds.
    /// </summary>
    public TimeSpan WorkerShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the global occurrence budget shared by all recurring cursors in one control-plane convergence
    /// cycle. Overdue cursors are revisited in durable due-time order until this budget is exhausted or the backlog is
    /// caught up. The default is 100.
    /// </summary>
    public int MaxRecurringMaterializationsPerCycle { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum expired execution leases recovered per control-plane cycle. The default is 100.
    /// </summary>
    public int MaxExpiredLeaseRecoveriesPerCycle { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether terminal execution history is periodically trimmed according to active job policies. The
    /// default is <see langword="true"/>.
    /// </summary>
    public bool EnableHistoryCleanup { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval after a partial durable history cleanup pass. Full batches continue on the next
    /// control-plane poll until the eligible backlog is drained. The default is one hour.
    /// </summary>
    public TimeSpan HistoryCleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum terminal executions deleted in one cleanup pass. The default is 5,000.
    /// </summary>
    public int MaxHistoryDeletionsPerCycle { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the maximum newest terminal executions retained for jobs absent from the active catalog. Zero
    /// disables count-based orphan cleanup. The default is 10.
    /// </summary>
    public int MaxRetainedOrphanedExecutions { get; set; } = 10;

    /// <summary>
    /// Gets or sets the serializer used for triggered-job argument payloads. The default preserves non-ASCII text.
    /// </summary>
    public JsonSerializerOptions JobArgsSerializerOptions { get; set; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Resolves the local project identity from explicit scheduler configuration, Monica application configuration,
    /// or the process entry assembly.
    /// </summary>
    public string GetProjectName()
    {
        return Application.ResolveProjectName(ProjectName, Assembly.GetEntryAssembly()?.GetName().Name);
    }
}

/// <summary>
/// Selects the scheduler responsibilities owned by the current host.
/// </summary>
public enum JobSchedulerRole
{
    ControlPlane,
    Worker,
    Standalone
}
