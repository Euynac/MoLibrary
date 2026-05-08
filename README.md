# Monica

<p align="center">
  <img src="logo.png" alt="Monica Logo" width="200" />
</p>

<p align="center">
  <a href="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml"><img src="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml/badge.svg" alt="Unit Tests"></a>
  <a href="https://www.nuget.org/packages?q=Monica"><img src="https://img.shields.io/nuget/v/Monica.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/Tairitsua/Monica/blob/main/LICENSE.txt"><img src="https://img.shields.io/github/license/Tairitsua/Monica" alt="License"></a>
  <a href="https://monica.dpdns.org/"><img src="https://img.shields.io/badge/docs-online-brightgreen.svg" alt="Documentation"></a>
  <a href="https://deepwiki.com/Tairitsua/Monica"><img src="https://deepwiki.com/badge.svg" alt="Ask DeepWiki"></a>
</p>

<p align="center">
  English | <a href="README.zh_CN.md">简体中文</a>
</p>

> **Mo**dular **.N**ET **I**nfrastructure for **C#** **A**I-era backends.
> Monica combines typed DDD ProjectUnits, composable infrastructure modules, built-in dashboards, and bundled agent skills so AI-assisted backend work stays observable as it grows.

> **Release candidate**: Monica 1.0.0-rc.1 is a pre-release for validation and feedback. Breaking changes may still happen before 1.0.0 stable.

## Quick Links

- Docs site: <https://monica.dpdns.org/>
- Monica.Docs example repo: <https://github.com/Tairitsua/Monica.Docs>
- JobScheduler guide: <https://monica.dpdns.org/markdown-docs?group=monica&document=modules%2Fjob-scheduler%2Findex.md&culture=zh-CN>
- Changelog: [CHANGELOG.md](CHANGELOG.md)


## Why Monica

- AI can produce code quickly, but without a shared spec the result becomes brittle and hard to observe as the system grows.
- Monica turns infrastructure into the spec: every module registers through the same `Mo.Add*()` pattern, and every backend feature is expressed as typed ProjectUnits instead of ad hoc glue.
- The bundled skills under `.claude/skills/` and `.agents/skills/` teach agents the Monica way before they write code.

## Demo

The one-minute demo walks through the operator-facing parts of Monica:

- JobScheduler health dashboard, recent activity, job definitions, and manual execution.
- Cron expression editing without wiring a custom admin page by hand.
- ModuleSystem performance and dependency views for the running host.
- Runtime configuration inspection and theme switching.


https://github.com/user-attachments/assets/250e1e5f-0a78-4b8b-b832-756d682a01bd

## JobScheduler in Code

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

- Recurring and triggered jobs share one scheduling model.
- Concurrency guards, zombie detection, and persistence options are built in.
- Add `Mo.AddJobSchedulerUI()` when you want the browser dashboard and job detail views. The UI requires an ASP.NET Core web host because it serves Blazor routes and static web assets; a plain console host is enough for scheduler-only jobs, but not for the dashboard.
- See [`examples/JobSchedulerMinimal`](examples/JobSchedulerMinimal) for the smallest verified ASP.NET Core host that runs JobScheduler + JobScheduler UI at `/job-scheduler`.

## Global Configuration

Configure Monica-wide defaults through root `Mo.Config*()` methods before registering modules:

```csharp
Mo.ConfigApplication(options =>
{
    options.AppName = "My Application";
    options.AppId = "my-application";
});

Mo.ConfigModuleSystem(options =>
{
    options.DefaultApiGroupName = "Core";
});
```

Module options still take precedence over these global defaults.

## Dashboards, Themes, and i18n

Monica ships multiple operational Blazor UIs on top of `Monica.UI`: JobScheduler, Configuration, DependencyInjection, ProjectUnits, ModuleSystem, AI, and more. The UI layer shares the same theme contract, several swappable theme variants, and shipped `en-US` + `zh-CN` localization resources.

## Bundled Agent Skills

The repo includes ready-to-use skill packs in `.claude/skills/` and `.agents/skills/`.

- Framework entry points: `monica-framework`, `monica-development`, `monica-architecture`, `monica-ui-development`, `monica-ui-design`, `monica-ui-audit`, `monica-docs-authoring`, `monica-requirement-design`, `monica-unit-testing`, `monica-ui-bridge-debug`
- Application entry points: `monica-application`, `monica-application-microservice`, `monica-application-modular-monolith`, `monica-application-project-unit-development`
- Supporting workflows: `code-simplifier`, `playwright-cli`, `subagent-progress-report`, `third-party-source-catalog`

## Module Catalog

Monica is organized as many small composable modules rather than a few large packages.

- Core infrastructure: `Core`, `Tool`, `DependencyInjection`, `ResultEnvelope`, `Mediator`, `JsonSerialization`, `Localization`
- DDD and application flow: `ProjectUnits`, `AutoController`, `AutoModel`, `Repository`, `UnitOfWork`
- Background and ops: `JobScheduler`, `Configuration`, `Logging`, `ObservableInstance`, `HostedService`, `ServiceDiscovery`, `Locker`, `Resilience`
- Communication and integration: `EventBus`, `SignalR`, `DataChannel`, `Dapr`, `Markdown`
- AI and analysis: `AI`, `RAG`, `Framework`, `Framework.UI`, `AI.UI`, `JobScheduler.UI`, `Configuration.UI`, `DependencyInjection.UI`
- Platform utilities: `WebApi`, `Validation`, `Office`, `Profiling`, `DevOps`, `K8S`, `Git`, `FileOps`, `Utilities`

> See the full module index in the docs site for the current authoritative list.

## Architecture at a Glance

### Module Pattern

- `Module{Name}Option`: public configuration surface
- `Module{Name}Guide`: fluent follow-up configuration
- `Module{Name}`: the module implementation
- `Module{Name}BuilderExtensions`: the `Mo.Add*()` entry point

### ProjectUnit Pattern

Typed DDD building blocks that define what AI is allowed to write: `ApplicationService`, `RequestDto`, `DomainService`, `Entity`, `Repository`, `DomainEvent`, `DomainEventHandler`, `LocalEventHandler`, `Configuration`, `RecurringJob`, `TriggeredJob`.

ProjectUnit guidance lives in `monica-application-project-unit-development` and in the Monica.Docs concept pages that explain the same vocabulary in context.

## Technology Stack

- [.NET 10](https://github.com/dotnet/runtime)
- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [MudBlazor](https://github.com/MudBlazor/MudBlazor)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Dapr](https://github.com/dapr/dapr)
- [Serilog](https://github.com/serilog/serilog)
- [FluentValidation](https://github.com/FluentValidation/FluentValidation)
- [Polly](https://github.com/App-vNext/Polly)

## Contributing

1. Fork the repository.
2. Create a branch.
3. Commit your changes.
4. Open a pull request.

Full contribution and release guidance is in [CONTRIBUTING.md](CONTRIBUTING.md).

## Acknowledgements

A small subset of Monica modules was informed by the [ABP Framework](https://github.com/abpframework/abp), which is licensed under LGPL-3.0. Any directly adapted code keeps its original notices and license terms. Monica is an independent project and is not affiliated with ABP.

## License

MIT License. See [LICENSE.txt](LICENSE.txt).

## Contact

- Issues: <https://github.com/Tairitsua/Monica/issues>
- Discussions: <https://github.com/Tairitsua/Monica/discussions>
- Docs: <https://monica.dpdns.org/>
- Security: [SECURITY.md](SECURITY.md)
