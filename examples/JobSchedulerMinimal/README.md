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
- one standalone JobScheduler using `UseInMemoryStore()`, an explicit scheduler scope, immutable release manifest, and local worker identity
- `monica.AddJobSchedulerUI()`
- web lifecycle: `app.UseMonica()` and `app.MapMonica()`

The app defines one recurring `MinimalHeartbeatJob` so the scheduler overview, catalog, and execution pages have a concrete job to display.

The in-memory store is for local development. With a durable production store, worker crash recovery is at-least-once: lease fencing protects scheduler state from stale workers, while job-owned side effects still need idempotency or transactional boundaries.
