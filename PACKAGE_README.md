# Monica Framework

Monica is **Mo**dular **.N**ET **I**nfrastructure for **C#** **A**I-era backends.

It provides typed DDD ProjectUnits, composable infrastructure modules, built-in Blazor dashboards, and bundled agent skills so AI-assisted backend work remains observable as a codebase grows.

## Release Status

`1.0.0-rc.1` is a release candidate for validation and feedback before the stable `1.0.0` release. Breaking changes may still happen before the stable release.

## Installation

Install only the modules you need:

```bash
dotnet add package Monica.Core --version 1.0.0-rc.1
dotnet add package Monica.JobScheduler --version 1.0.0-rc.1
dotnet add package Monica.JobScheduler.UI --version 1.0.0-rc.1
```

## JobScheduler Example

```csharp
using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.Modules;

Mo.AddJobScheduler()
    .UseInMemoryMetadataRepository()
    .UseSchedulerScope("local-dev")
    .UseInMemoryProvider();

[JobConfig(
    JobName = "Heartbeat",
    Description = "Writes a heartbeat every five minutes.",
    CronSchedule = "0 */5 * * * *")]
public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger) : RecurringJob
{
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Heartbeat job ran.");
        return Task.CompletedTask;
    }
}
```

Add `Mo.AddJobSchedulerUI()` when you want the browser dashboard.

## Module Families

- Core infrastructure: `Monica.Core`, `Monica.Tool`, `Monica.DependencyInjection`
- DDD and application flow: `Monica.Core`, `Monica.Repository`, `Monica.WebApi`, `Monica.AutoModel`
- Background and ops: `Monica.JobScheduler`, `Monica.Configuration`, `Monica.Logging`, `Monica.StateStore`
- UI modules: `Monica.UI`, `Monica.Framework.UI`, `Monica.JobScheduler.UI`, `Monica.Configuration.UI`
- AI and integration: `Monica.AI`, `Monica.Dapr`, `Monica.EventBus`, `Monica.SignalR`
- Code generation: `Monica.Framework.Generators`, `Monica.Generators.AutoController`

## Documentation

- Documentation site: https://monica.dpdns.org/
- Repository: https://github.com/Tairitsua/Monica
- Example docs host: https://github.com/Tairitsua/Monica.Docs
- Issues: https://github.com/Tairitsua/Monica/issues

## License

MIT License. See `LICENSE.txt` in the repository.
