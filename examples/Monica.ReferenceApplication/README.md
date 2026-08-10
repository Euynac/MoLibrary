# Monica Ordering Reference

A runnable ordering backend that demonstrates Monica's Stable application path while preserving normal ASP.NET Core hosting and domain-first ownership.

The sample is intentionally more realistic than a hello-world endpoint:

- one composition-only AppHost and one ordering bounded context
- stable request, response, endpoint, and event contracts in `Platform.Protocol`
- rich order invariants owned by the entity
- a replaceable in-memory repository for zero-infrastructure startup
- request-owned API contracts exposed by command and query `ApplicationService` units
- a schema-first `[Configuration]` ProjectUnit consumed through `IOptions<OrderingOptions>`
- a unit-of-work boundary around controller requests
- an `EventOrderApproved` local event published only after unit-of-work completion
- an auto-discovered `LocalEventHandlerOrderApproved` reaction
- an auto-discovered recurring backlog report backed by the in-memory job scheduler
- Swagger, ProjectUnit inspection, Prometheus metrics, and a health response

No database, broker, cache, or other external service is required. Runtime configuration documents are stored under the application output directory, while the repository and scheduler metadata reset with the process or rebuild output.

## Run

From the Monica repository root:

```bash
dotnet run --project examples/Monica.ReferenceApplication/src/AppHost/Monica.Reference.Api/Monica.Reference.Api.csproj
```

The launch profile listens on <http://localhost:5275>. The repository starts with two draft orders, so the first request already returns useful data:

```bash
curl http://localhost:5275/api/v1/Ordering/orders
```

Create an order:

```bash
curl --request POST http://localhost:5275/api/v1/Ordering/orders \
  --header 'Content-Type: application/json' \
  --data '{"orderNumber":"MON-1003","customerName":"Adventure Works","total":1299.90}'
```

Approve it using the identifier returned by the create request:

```bash
curl --request POST http://localhost:5275/api/v1/Ordering/orders/approve \
  --header 'Content-Type: application/json' \
  --data '{"orderId":"REPLACE_WITH_ORDER_ID"}'
```

Explore the running architecture:

- Swagger UI: <http://localhost:5275/swagger>
- Monica ProjectUnit catalog: <http://localhost:5275/framework/units>
- Prometheus metrics: <http://localhost:5275/metrics>
- Readiness response: <http://localhost:5275/health>
- Liveness response: <http://localhost:5275/alive>

`OrderingOptions.MaximumOrdersReturned` limits the collection response. The values in `appsettings.json` seed Monica's local effective-value store on first startup, and the query consumes the resulting configuration through `IOptions<OrderingOptions>`.

The backlog worker runs once per minute. Its execution history and normal application log report the draft count, escalating to a warning when `OrderingOptions.BacklogWarningThreshold` is reached.

## Request-owned API contracts

Published endpoints are declared beside their request contracts in `Platform.Protocol`, not on the handlers that execute them:

```csharp
[ApiEndpoint(
    ApiHttpMethod.Post,
    "orders",
    Binding = ApiRequestBinding.Body)]
public sealed record CommandCreateOrder(
    string OrderNumber,
    string CustomerName,
    decimal Total) : IResultRequest<OrderDto>;
```

`WebApiGenerationConfig` supplies the shared `api/v1/Ordering` prefix and asks the generator for the published HTTP RPC client. The `Command` prefix classifies the operation and produces the default `CreateOrder` method name. The corresponding `CommandHandlerCreateOrder` contains only execution logic; it does not repeat the route, verb, or binding attributes. Requests under `PublishedLanguages.DomainOrdering.Requests` are eligible for RPC clients. An attributed request outside that published-language namespace can still expose a local HTTP endpoint, but it remains intentionally absent from the generated RPC contract.

## Host-bound module graph

The AppHost declares its Configuration inputs once, then records its runtime capabilities in one `AddMonica` callback. The immutable plan keeps the selected store, source order, and section-path convention consistent wherever startup configuration and runtime composition need them:

```csharp
var configurationInputPlan = MonicaConfigurationInputPlan.Create(inputs => inputs
    .UseFileConfigurationStore());

builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.ProjectName = "Monica.Reference.Api";
        options.AppName = "Monica Ordering Reference";
        options.AppId = "monica-ordering-reference";
        options.DomainName = "Ordering";
    });
    monica.ConfigureModuleSystem(options =>
    {
        options.DefaultApiGroupName = "Ordering";
        options.EnableMinimalApiByDefault = true;
    });
    monica.ConfigureTypeDiscovery(options =>
        options.Add("Domains.Ordering", "Platform.Protocol"));

    monica.AddHealthCheck();
    monica.AddConfiguration(configurationInputPlan);
    monica.AddEventBus()
        .UseNoOpDistributedEventBus();
    monica.AddResultEnvelope();
    monica.AddWebApi();
    monica.AddSwagger(options =>
    {
        options.AppName = "Monica Ordering Reference";
        options.ApiVersion = "v1";
    });
    monica.AddUnitOfWork();
    monica.AddProjectUnits(options =>
    {
        options.ConventionOptions.EnableNameConvention = true;
        options.ConventionOptions.NameConventionMode = ENameConventionMode.Strict;
    });
    monica.AddJobScheduler(options =>
        {
            options.MaxWorkerExecutionThreads = 1;
        })
        .UseInMemoryMetadataRepository()
        .UseSchedulerScope("monica-reference-ordering")
        .UseInMemoryProvider();
    monica.AddOpenTelemetry()
        .UsePrometheusEndpoint();
});
```

The no-op distributed provider makes the external integration boundary explicit without introducing a broker. `EventOrderApproved` is dispatched through the real in-process local bus. The approval handler registers publication with `IUnitOfWork.OnCompleted`, so the local reaction observes only a successfully completed request boundary.

JobScheduler UI is deliberately not composed: adding its Blazor shell to an API-only host would blur the host boundary. The recurring job remains discoverable in the ProjectUnit catalog and observable through scheduler execution history and logs.

## Architecture

```text
src/
├── AppHost/Monica.Reference.Api/       # composition only
├── Domains/Ordering/                   # use cases, events, job, entity, repository
└── Shared/
    ├── Platform.Infrastructure/        # solution-owned infrastructure boundary
    ├── Platform.Protocol/              # stable business language
    └── Platform.BuildingBlocks/        # project-common Monica capabilities
```

The project-reference direction is deliberately one-way:

```text
AppHost -> Ordering -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks
```

That shape gives humans and coding agents a predictable answer to “where does this change belong?” without splitting a small deployment into premature microservices.

## Package maturity

This reference stays entirely on Monica's Stable path:

| Tier | Used here | Guidance |
| --- | --- | --- |
| Stable | Core composition, Configuration, EventBus, Web API, UnitOfWork, ProjectUnits, JobScheduler, repository abstractions, OpenTelemetry | Appropriate defaults for an application foundation |
| Integrations | None | Add a database, broker, cache, or service provider only when the deployment has chosen it |
| Labs | None | Evaluate runtime AI, RAG, MCP, DataChannel, DevOps, Office, and profiling packages explicitly |

`RepositoryOrder` is the intentional persistence seam. A production application can replace it with an Integration-tier provider without moving order rules out of the domain entity or changing the public requests. Because this sample repository is process-local rather than transactional, UnitOfWork demonstrates request coordination and post-completion behavior; it does not pretend to roll back in-memory mutations.
