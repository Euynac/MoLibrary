# MonicaStarter

An observable ASP.NET Core API composed through one host-bound Monica module graph.

## Run

```bash
dotnet restore
dotnet run
```

Use the URL printed by ASP.NET Core:

- `/` describes the running starter
- `/health` reports readiness and `/alive` reports process liveness
- `/metrics` exposes Prometheus metrics

## Grow deliberately

The starter begins with Stable-tier `Monica.Core`, `Monica.HealthCheck`, and `Monica.OpenTelemetry` packages. Add a provider from the Integrations tier only after choosing an infrastructure dependency. Add Labs packages only for capabilities whose experimental lifecycle is acceptable to your application.

Keep all Monica module composition inside the `AddMonica` callback. The graph is completed and validated for this host before ASP.NET Core builds the application.
