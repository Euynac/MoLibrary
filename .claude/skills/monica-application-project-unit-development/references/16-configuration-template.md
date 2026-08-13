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
var releaseId = builder.Configuration["JobScheduler:ReleaseId"]
                ?? throw new InvalidOperationException("JobScheduler:ReleaseId is required.");
var deploymentGeneration = builder.Configuration.GetValue<long?>("JobScheduler:DeploymentGeneration")
                           ?? throw new InvalidOperationException(
                               "JobScheduler:DeploymentGeneration is required.");
var workerRevisionId = builder.Configuration["JobScheduler:WorkerRevisionId"]
                       ?? throw new InvalidOperationException("JobScheduler:WorkerRevisionId is required.");

builder.AddMonica(monica =>
{
    monica.AddConfiguration();
    monica.AddJobScheduler(options =>
        {
            options.ProjectName = "Ordering";
            options.MaxWorkerExecutionThreads = workerCount;
        })
        .AsStandalone()
        .UseInMemoryStore()
        .UseSchedulerScope("ordering")
        .UseCatalogRelease(
            releaseId,
            deploymentGeneration,
            [new("Ordering", workerRevisionId)])
        .UseLocalWorkerIdentity("Ordering", workerRevisionId);
});
```

## Notes

- Use configuration for environment- or host-specific behavior, not for domain constants that belong in code.
- Keep option names explicit and developer-facing.
- Do not build a temporary service provider or resolve `IOptions<T>` during composition. Runtime code should use normal typed options injection.
- `UseInMemoryStore()` is the single unified catalog-and-execution store for local standalone hosts. Production replicas must select one shared durable `IJobSchedulerStore`, normally through `UseEfCoreStore(...)` with PostgreSQL.
- Treat the release ID, deployment generation, complete owner manifest, and worker revision as deployment identity. Every host in one release must receive identical manifest values, and the deployment generation must increase monotonically for the lifetime of the scheduler database.
