# JobScheduler Minimal

Minimal ASP.NET Core host for Monica JobScheduler plus the JobScheduler UI.

## Run

```bash
dotnet run --project examples/JobSchedulerMinimal/JobSchedulerMinimal.csproj
```

Open <http://127.0.0.1:5298/job-scheduler>.

The launch profile uses the `Development` environment so Blazor static web assets from the referenced UI modules are available during `dotnet run`.

## What It Registers

- one `builder.AddMonica(monica => ...)` host boundary
- `monica.AddJobScheduler().UseInMemoryMetadataRepository().UseSchedulerScope("job-scheduler-minimal").UseInMemoryProvider()`
- `monica.AddJobSchedulerUI()`
- web lifecycle: `app.UseMonica()` and `app.MapMonica()`

The app defines one recurring `MinimalHeartbeatJob` so the dashboard and job definitions page have a concrete job to display.
