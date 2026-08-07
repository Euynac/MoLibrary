# Monica.OpenTelemetry

Monica.OpenTelemetry wires the OpenTelemetry .NET SDK for Monica hosts without adding exporter dependencies to infrastructure modules.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.AddOpenTelemetry(options => options.ResourceServiceName = "MyApp")
        .UseOtlpExporter()
        .UsePrometheusEndpoint();
});

var app = builder.Build();
app.UseMonica();
app.MapMonica();
app.Run();
```

The module subscribes to `Monica.*` meters by default and can also expose a bounded in-process metric snapshot through:

```csharp
builder.AddMonica(monica =>
{
    monica.AddOpenTelemetry()
        .UseInProcessCollector();
});
```

The in-process collector is per application instance, bounded by `SamplesPerSeries` and `MaxTagSetsPerInstrument`, and is intended for local diagnostics or the `Monica.OpenTelemetry.UI` dashboard. Use OTLP or Prometheus for production retention and multi-instance analysis.

Built-in ASP.NET Core, HttpClient, and runtime metrics are enabled through the OpenTelemetry SDK instrumentation flags. When the in-process collector is enabled, Monica also subscribes the dashboard collector to the matching built-in meters by default, so those SDK instrumentations are visible locally. Disable that behavior when the dashboard should only show explicitly configured meter patterns:

```csharp
builder.AddMonica(monica =>
{
    monica.AddOpenTelemetry()
        .UseInProcessCollector()
        .UseInstrumentationMetersInProcessCollector(false);
});
```
