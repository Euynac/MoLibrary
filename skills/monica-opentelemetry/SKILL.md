---
name: monica-opentelemetry
description: Monica OpenTelemetry and metrics development guidance. Use when adding or refactoring Monica module telemetry, creating a Metrics/ folder, designing System.Diagnostics.Metrics instruments with IMeterFactory, exposing OpenTelemetry-compatible meter names, choosing Counter/Histogram/Gauge instruments, handling app-owned diagnostic snapshots, avoiding high-cardinality tags, or deciding where metrics instrumentation belongs in Monica modules.
---

# Monica OpenTelemetry

Use this skill to design Monica module telemetry that is compatible with OpenTelemetry without making infrastructure modules depend on OpenTelemetry exporters.

## Core Rule

Monica modules emit standard .NET metrics through `System.Diagnostics.Metrics`. Hosts configure OpenTelemetry exporters, Prometheus scraping, OTLP, Azure Monitor, storage, and dashboards.

```text
Monica module -> System.Diagnostics.Metrics -> host OpenTelemetry setup -> backend/UI
```

Do not add `OpenTelemetry.*` package references to Monica infrastructure modules unless the module is explicitly an observability host/integration module.

## Folder Placement

Use `Metrics/` for telemetry names, instruments, app-owned metric state, and instrumentation adapters.

```text
Monica.{Name}/
├── Metrics/
│   ├── {Name}MetricNames.cs             # public constants when hosts need meter names
│   ├── {Feature}Metrics.cs              # internal singleton metric owner
│   └── {Feature}MetricsInterceptor.cs   # optional adapter, e.g. EF Core interceptor
```

For feature-folder modules, place telemetry under the feature:

```text
Monica.Repository/
└── Persistence/
    └── Metrics/
```

Snapshot DTOs exposed to Facades or UI stay in `Models/`, not `Metrics/`.

## Standard Pattern

Create one internal singleton metric owner per coherent telemetry area. Use `IMeterFactory`, not static `new Meter(...)`, in DI-based Monica modules.

```csharp
using System.Diagnostics.Metrics;

namespace Monica.Repository.Persistence.Metrics;

public static class RepositoryPersistenceMetricNames
{
    public const string MeterName = "Monica.Repository.Persistence";
    public const string EfCoreConnectionEvents =
        "monica.repository.persistence.ef_core.connection.events";
}

internal sealed class EfCoreConnectionMetrics(IMeterFactory meterFactory)
{
    private const string EVENT_TAG_NAME = "event";

    private readonly Counter<long> _connectionEvents = meterFactory
        .Create(RepositoryPersistenceMetricNames.MeterName)
        .CreateCounter<long>(
            RepositoryPersistenceMetricNames.EfCoreConnectionEvents,
            unit: "events",
            description: "EF Core connection lifecycle events observed by Monica Repository.");

    public void RecordConnectionOpened()
    {
        _connectionEvents.Add(1, new KeyValuePair<string, object?>(EVENT_TAG_NAME, "opened"));
    }
}
```

Register metric owners as singleton:

```csharp
services.TryAddSingleton<EfCoreConnectionMetrics>();
services.TryAddSingleton<EfCoreConnectionMetricsInterceptor>();
```

## Host Bootstrap

`IMeterFactory` is auto-registered by `Microsoft.Extensions.Hosting` in .NET 8+ and by ASP.NET Core hosts. Monica modules just register their metric owners with `TryAddSingleton<TMetrics>()`. Call `services.AddMetrics()` only in non-Hosting scenarios such as bare `ServiceCollection` test fixtures.

Expose a module option when instrumentation has non-trivial overhead or attaches framework adapters:

```csharp
public bool EnableEfCoreConnectionMetrics { get; set; }
```

## Instrument Choice

- Use `Counter<T>` for monotonic totals such as requests, failures, lifecycle events, bytes sent, or jobs completed.
- Use `Histogram<T>` for distributions such as duration, payload size, queue latency, or retry delay.
- Use `ObservableGauge<T>` for current values read from app-owned state, such as active jobs, pending sends, or open connections.
- Use app-owned state only when Monica needs a current snapshot, UI data, or observable gauge values. Do not mirror counters just to read metric values later.

## Tags

Keep tags low-cardinality and stable.

Good tags:

```text
event=opened
result=success
reason=validation
state=pending
provider=postgresql
```

Avoid tags with unbounded or sensitive values:

```text
user.id
order.id
trace.id
connection.id
exception.message
raw path/query strings
```

Use private constants for tag names and stable tag values. Private constants use upper snake case in Monica.

## App-Owned Diagnostics vs Telemetry

Telemetry instruments are write/observe APIs, not the source of truth for app logic.

Use this split:

```text
Metric owner state
  -> only when observable gauges or Monica snapshots need current values

Metric instruments
  -> emit Counter/Histogram/Gauge measurements

Snapshot models
  -> public Models/ types returned by Facades/UI
```

For UI diagnostics like SignalR pending sends, keep the state owner cohesive and expose snapshots through a Facade. Emit OpenTelemetry-compatible metrics from the same owner only when useful.

Keep app-owned fields only when an `ObservableGauge`, snapshot model, or Monica runtime behavior reads them. Do not duplicate `Counter<T>` values into local `Interlocked` fields just because the event also increments.

## Performance Rules

- Metric calls should stay on hot paths only when they do minimal work.
- Avoid locks in metric recording; use `Interlocked` only when app-owned state is required.
- Avoid large tag sets; `Add`/`Record` with three or fewer individually supplied tags is preferred for hot paths.
- Keep observable callbacks fast: read cached state and return measurements. Do not perform I/O, database calls, service calls, or blocking work.
- Do not register custom `MeterListener` aggregation in Monica modules unless explicitly designing a collector.

## Host Configuration Boundary

Monica modules should document the meter name and emit metrics. A host can subscribe with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(RepositoryPersistenceMetricNames.MeterName);
        metrics.AddPrometheusExporter();
    });
```

The host maps scraping/export endpoints and configures storage/UI. Monica infrastructure modules do not.
