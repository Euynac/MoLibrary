# Configuration Template

Use `$DomainNamespace$` for the domain project namespace selected by the architecture skill, such as `OrderingService.Domain` or `Domains.Ordering`.

## Use When

- A feature needs host-provided runtime configuration.
- Consumers should receive typed options through dependency injection.

## Rules

- Mark the class with `[Configuration]`.
- End the class name with `Options`.
- Use `IOptions<T>` for mostly static configuration.
- Use `IOptionsSnapshot<T>` for per-scope refreshed values.
- Use `IOptionsMonitor<T>` for long-lived services that react to changes.
- Register the Configuration module in the host-bound graph. If composition code needs a bootstrap value, read it from `builder.Configuration` and pass the explicit value into the dependent module option; Configuration ProjectUnits are runtime services, not composition-time service-locator state.

## Options Class Example

```csharp
using Monica.Configuration.Annotations;
using Monica.ProjectUnits.Annotations;

namespace $DomainNamespace$.Configurations;

[Configuration]
[ProjectUnitMetadata(
    "Order Processing Configuration",
    Owner = "$Owner$",
    Description = "Controls host-specific order processing behavior.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class OrderProcessingOptions
{
    public bool AutoApproveEnabled { get; set; }

    public int ApprovalBatchSize { get; set; } = 100;
}
```

## Consumer Example

```csharp
using Microsoft.Extensions.Options;
using Monica.ProjectUnits.Annotations;
using Monica.WebApi.Abstractions;

namespace $DomainNamespace$.DomainServices;

[ProjectUnitMetadata(
    "Order Approval Rules",
    Owner = "$Owner$",
    Description = "Evaluates configured order approval behavior.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class DomainOrderApproval(
    IOptions<OrderProcessingOptions> options)
    : DomainService
{
    private OrderProcessingOptions Options => options.Value;

    public bool IsAutoApproveEnabled()
    {
        return Options.AutoApproveEnabled;
    }
}
```

## Host Composition Example

```csharp
var builder = WebApplication.CreateBuilder(args);
var workerCount = builder.Configuration.GetValue<int?>("Ordering:WorkerCount") ?? 4;

builder.AddMonica(monica =>
{
    monica.AddConfiguration();
    monica.AddJobScheduler(options =>
        {
            options.ProjectName = "Ordering";
            options.MaxWorkerExecutionThreads = workerCount;
        })
        .UseInMemoryStore()
        .UseSchedulerScope("ordering");
});
```

## Notes

- Use configuration for environment- or host-specific behavior, not for domain constants that belong in code.
- Keep option names explicit and developer-facing.
- Do not build a temporary service provider or resolve `IOptions<T>` during composition. Runtime code should use normal typed options injection.
- `UseInMemoryStore()` is the process-local store for single-host development and deterministic tests. Production hosts must select one shared durable `IJobSchedulerStore`, normally through `UseEfCoreStore(...)` with PostgreSQL.
- Every host schedules and executes the jobs it discovers under its `ProjectName` owner identity inside `UseSchedulerScope(...)`. Multiple replicas using the same declaration snapshot are safe: definition snapshot sync, recurring cursor materialization, and queue claims are all store-level compare-and-swap operations. Do not overlap incompatible declaration snapshots for one owner; the current protocol has no publisher retirement handshake, so changed job sets or contracts require a drain or blue-green owner strategy. Different services may reuse the same job key because definitions, cursors, and gates are keyed by `(SchedulerScopeKey, OwnerKey, JobKey)`.
- No deployment identity (release IDs, deployment generations, owner manifests, worker revisions) is required. A host that starts with a stable scope starts scheduling immediately; definitions absent from its latest snapshot are marked absent and reject new admission while their operator policy is retained for audit.
