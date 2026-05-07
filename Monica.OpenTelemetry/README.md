# Monica.OpenTelemetry

Monica.OpenTelemetry wires the OpenTelemetry .NET SDK for Monica hosts without adding exporter dependencies to infrastructure modules.

```csharp
Mo.AddOpenTelemetry(options => options.ServiceName = "MyApp")
    .UseOtlpExporter()
    .UsePrometheusEndpoint();
```

The module subscribes to `Monica.*` meters by default and can also expose a bounded in-process metric snapshot through:

```csharp
Mo.AddOpenTelemetry()
    .UseInProcessCollector();
```

The in-process collector is per application instance, bounded by `SamplesPerSeries` and `MaxTagSetsPerInstrument`, and is intended for local diagnostics or the `Monica.OpenTelemetry.UI` dashboard. Use OTLP or Prometheus for production retention and multi-instance analysis.
