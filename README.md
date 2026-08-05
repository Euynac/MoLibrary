# Monica

<p align="center">
  <img src="logo.png" alt="Monica Logo" width="176" />
</p>

<p align="center">
  <strong>Architecture agents can follow. Systems humans can inspect.</strong>
</p>

<p align="center">
  <a href="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml"><img src="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml/badge.svg" alt="Build"></a>
  <a href="https://www.nuget.org/packages?q=Monica"><img src="https://img.shields.io/nuget/v/Monica.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/Tairitsua/Monica/blob/main/LICENSE.txt"><img src="https://img.shields.io/github/license/Tairitsua/Monica" alt="License"></a>
  <a href="https://monica.dpdns.org/"><img src="https://img.shields.io/badge/docs-monica.dpdns.org-6d5ce7.svg" alt="Documentation"></a>
</p>

<p align="center">
  English | <a href="README.zh_CN.md">简体中文</a>
</p>

Monica is an agent-governed application architecture for observable .NET backends. It gives developers and coding agents the same typed vocabulary for modules, DDD application units, infrastructure, and runtime diagnostics—so generated code remains structurally predictable and the running system remains understandable.

> Monica is approaching 1.0 and is still allowed to make breaking architectural improvements before the stable release.

## Why Monica

- **A graph, not startup glue.** `AddMonica(...)` records the complete module graph for one host, validates dependencies and cycles, then applies registrations in deterministic phases.
- **A vocabulary agents can follow.** ProjectUnits describe application services, domain services, entities, repositories, events, handlers, configurations, and jobs as explicit architectural roles.
- **Inspectable by default.** Operational modules expose the same graph, configuration, jobs, telemetry, and application structure that produced the running process.
- **Host-scoped state.** A Monica composition belongs to its host. Multiple hosts in one process do not share module registries, options, mapping rules, or ProjectUnit catalogs.

## Start in one minute

Install only the packages used by your application:

```bash
dotnet add package Monica.ProjectUnits --prerelease
dotnet add package Monica.JobScheduler --prerelease
dotnet add package Monica.JobScheduler.UI --prerelease
```

Compose the host in one bounded callback:

```csharp
using Monica.Core.Modularity.Extensions;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.AppName = "Orders";
        options.AppId = "orders";
    });

    monica.ConfigureModuleSystem(options =>
    {
        options.DefaultApiGroupName = "Orders";
    });

    monica.AddProjectUnits(options =>
    {
        options.ConventionOptions.EnableNameConvention = true;
    });

    monica.AddJobScheduler(options =>
        {
            options.ProjectName = "Orders";
        })
        .UseInMemoryMetadataRepository()
        .UseSchedulerScope("orders")
        .UseInMemoryProvider();

    monica.AddJobSchedulerUI();
});

var app = builder.Build();
app.UseMonica();
app.MapMonica();
app.Run();
```

The callback is the complete composition boundary. Module guides cannot mutate the graph after it closes, and startup fails early when the graph is incomplete or cyclic.

For a runnable DDD application rather than a toy snippet, see [`examples/Monica.ReferenceApplication`](examples/Monica.ReferenceApplication). For the smallest dashboard host, see [`examples/JobSchedulerMinimal`](examples/JobSchedulerMinimal).

## The architecture contract

Every module follows the same public shape:

- `Module{Name}Option` — host-owned configuration and defaults.
- `Module{Name}Guide` — fluent provider and capability choices.
- `Module{Name}` — registration, dependency claims, middleware, and endpoints.
- `IMonicaBuilder` extension — the discoverable `monica.Add{Name}()` entry point.

ProjectUnits add the application vocabulary:

`ApplicationService` · `RequestDto` · `DomainService` · `Entity` · `Repository` · `DomainEvent` · `DomainEventHandler` · `LocalEventHandler` · `Configuration` · `RecurringJob` · `TriggeredJob`

Each discovered unit can also carry explicit agent context and requirement traceability:

```csharp
[ProjectUnitMetadata(
    "Approve Order",
    Owner = "Ordering Team",
    Description = "Approves an eligible order.",
    Tags = ["ordering", "approval"])]
[ProjectUnitRequirement("ORD-REQ-001")]
public sealed class CommandHandlerApproveOrder : ApplicationService<CommandApproveOrder>
{
    // ...
}
```

`Monica.Framework.UI` exposes a first-tab status dashboard for the current host with unit distribution, dependency health, alerts, and independent metadata, description, ownership, and requirement coverage. The typed `/framework/units`, `/framework/units/dashboard`, and `/framework/units/{key}` APIs expose the same catalog without leaking reflection objects.

The canonical Monica-owned Agent Skills live under [`skills/`](skills/). Release tooling projects that tree byte-for-byte into `.agents/skills/` and `.claude/skills/` for repository-local discovery; those generated directories are not authoring sources. Start with `monica-guide` for setup and diagnostics, then continue through the profile-selected `monica-application`, `monica-framework`, and granular skills.

## Third-party ecosystem

Independent packages use the publisher-first ID `<Publisher>.Monica.<Package>[.<Variant>]`; one NuGet package may contain any coherent number of infrastructure, provider, web, and UI modules. The official `Monica.*` prefix and purple logo remain reserved for first-party packages.

Start with the [`monica-third-party-module-development`](skills/monica-third-party-module-development) skill to scaffold, validate, test, pack, and publish an independent package. The compatibility identity and usage rules are summarized in [BRANDING.md](BRANDING.md), with the complete bilingual guide in [Monica.Docs](https://monica.dpdns.org/).

## Package maturity

The maturity label describes the compatibility promise, not package quality.

| Tier | Promise | Representative packages |
|---|---|---|
| **Stable** | The supported 1.0 application path | Core, ProjectUnits, WebApi, Configuration, Repository, JobScheduler, OpenTelemetry, UI |
| **Integrations** | Versioned adapters around external systems | EF Core, Kafka, Redis/StackExchange, Dapr, SignalR |
| **Labs** | Deliberately fast-moving exploration | AI/RAG/MCP, DataChannel, DevOps and profiling, Office, Experimental |

Applications can adopt Stable without taking a dependency on Labs. Integration packages remain optional and provider-specific; CI validates both complete package classification and one-way maturity dependencies.

## Runtime surfaces

Monica includes operational Blazor surfaces for module dependencies, ProjectUnits, configuration, jobs, dependency injection, telemetry, repositories, and other registered capabilities. They share a theme contract and first-party `en-US` and `zh-CN` localization resources.

The one-minute demo shows the JobScheduler dashboard, cron editing, module dependency inspection, and runtime configuration:

https://github.com/user-attachments/assets/250e1e5f-0a78-4b8b-b832-756d682a01bd

## Documentation and community

- Documentation: <https://monica.dpdns.org/>
- Read-only documentation API: <https://api.monica.dpdns.org/>
- Example documentation host: <https://github.com/Tairitsua/Monica.Docs>
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- Issues: <https://github.com/Tairitsua/Monica/issues>
- Discussions: <https://github.com/Tairitsua/Monica/discussions>
- Security: [SECURITY.md](SECURITY.md)

Contribution and release guidance is in [CONTRIBUTING.md](CONTRIBUTING.md).

## License and acknowledgements

Monica is MIT licensed. See [LICENSE.txt](LICENSE.txt).

A small subset of Monica modules was informed by the [ABP Framework](https://github.com/abpframework/abp), licensed under LGPL-3.0. Directly adapted code retains its original notices. Monica is an independent project and is not affiliated with ABP.
