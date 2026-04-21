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
- Use `$ApplicationNamespace$` for the application-layer namespace chosen by the architecture skill.
- Place jobs in `BackgroundWorkers/`.

## RecurringJob Example

```csharp
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;

namespace $ApplicationNamespace$.BackgroundWorkers;

[JobConfig(CronSchedule = "0 */5 * * * *", RetryCount = 3)]
public sealed class WorkerRefreshOrderSnapshot(
    DomainRefreshOrderSnapshot domainService)
    : RecurringJob
{
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(cancellationToken);
    }
}
```

## TriggeredJob Example

```csharp
using Monica.JobScheduler.Abstractions;

namespace $ApplicationNamespace$.BackgroundWorkers;

public sealed class RefreshOrderSnapshotArgs
{
    public long OrderId { get; init; }
}

public sealed class JobRefreshOrderSnapshot(
    DomainRefreshOrderSnapshot domainService)
    : TriggeredJob<RefreshOrderSnapshotArgs>
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
