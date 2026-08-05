# Job Template

## Use When

- Work should run on a schedule.
- Work should be triggered asynchronously with typed parameters.
- The business logic is long-running or retriable enough that it should not stay inside an event handler or request handler.

## Rules

- Use `RecurringJob` for scheduled work and name it `Worker*`.
- Use `TriggeredJob<TArgs>` for on-demand asynchronous work and name it `Job*`.
- Keep the job focused on scheduling, retry, and orchestration. Put reusable business behavior in a `DomainService`.
- Add `[JobConfig]` only when defaults are not enough.
- Inject `ILogger<TConcreteJob>` and pass it to the job base constructor.
- Use `$ApplicationNamespace$` for the application-layer namespace chosen by the architecture skill.
- Place jobs in `BackgroundWorkers/`.

## RecurringJob Example

```csharp
using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.ProjectUnits.Annotations;

namespace $ApplicationNamespace$.BackgroundWorkers;

[JobConfig(CronSchedule = "0 */5 * * * *", RetryCount = 3)]
[ProjectUnitMetadata(
    "Refresh Order Snapshot Worker",
    Owner = "$Owner$",
    Description = "Refreshes order snapshots on a fixed schedule.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class WorkerRefreshOrderSnapshot(
    DomainRefreshOrderSnapshot domainService,
    ILogger<WorkerRefreshOrderSnapshot> logger)
    : RecurringJob(logger)
{
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(cancellationToken);
    }
}
```

## TriggeredJob Example

```csharp
using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.ProjectUnits.Annotations;

namespace $ApplicationNamespace$.BackgroundWorkers;

public sealed class RefreshOrderSnapshotArgs
{
    public long OrderId { get; init; }
}

[ProjectUnitMetadata(
    "Refresh Order Snapshot Job",
    Owner = "$Owner$",
    Description = "Refreshes one order snapshot asynchronously.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class JobRefreshOrderSnapshot(
    DomainRefreshOrderSnapshot domainService,
    ILogger<JobRefreshOrderSnapshot> logger)
    : TriggeredJob<RefreshOrderSnapshotArgs>(logger)
{
    public override async Task ExecuteAsync(
        RefreshOrderSnapshotArgs parameters,
        CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(parameters.OrderId, cancellationToken);
    }
}
```

## Notes

- Keep job argument types simple and serializable.
- If a job does not need its own identity, scheduling metadata, or retry policy, an event handler may be sufficient.
