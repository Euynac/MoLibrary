# Monica.Testing

`Monica.Testing` is the shared testing toolkit for Monica framework projects and Monica-based business services. Runnable tests live in `tests/Test.Monica.*` or business `Test.*` projects.

## Complete Host Scenarios

Derive a reusable composition recipe from `MonicaTestApplicationFactory<TDiscoveryAnchor>`:

```csharp
public sealed class OrdersTestApplicationFactory
    : MonicaTestApplicationFactory<RefreshOrdersApplicationService>
{
    protected override void ConfigureHost(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(TestConfiguration.Values);
    }

    protected override void ConfigureMonica(IMonicaBuilder monica)
    {
        monica.AddOrders();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.UseTestDatabase<OrdersDbContext>();
    }
}
```

Each `CreateAsync(...)` call creates and starts one complete host:

```csharp
var gateway = Substitute.For<IOrdersGateway>();

await using var application = await factory.CreateAsync(
    scenario => scenario.With<IOrdersGateway>(gateway),
    TestContext.Current.CancellationToken);
await using var scope = application.CreateScope(TestContext.Current.CancellationToken);

var service = scope.Resolve<RefreshOrdersApplicationService>();
await service.ExecuteAsync(scope.CancellationToken);
```

Factory hooks have distinct ownership:

- `ConfigureHost(WebApplicationBuilder)` supplies host configuration and environment inputs.
- `TypeDiscoveryAssemblies` selects production assemblies beyond the default anchor assembly when a scenario spans projects.
- `ConfigureMonica(IMonicaBuilder)` composes the production modules exercised by the scenario.
- `ConfigureServices(IServiceCollection)` registers stable test providers and boundary doubles after module registration but before build.
- `CreateAsync(Action<ISeamReplacementBuilder>?, CancellationToken)` applies final scenario-specific registration replacements before build.

`MonicaTestApplication` exposes its root `Services`, host-owned `Application`, immutable `ModuleSnapshots`, and `CreateScope(CancellationToken)`. A `MonicaTestScope` resolves scoped services, carries the test cancellation token, and provides database seeding helpers. Scope creation never changes registrations.

## Ownership Rules

- One created application owns one host, one `MonicaApplication`, one singleton graph, and all of its scopes.
- Dispose scopes before their application.
- Create another application when a test needs different registrations.
- Never copy descriptors from a built host or attach one `MonicaApplication` to another root provider.
- Keep mutable doubles and external resource names isolated per scenario.
- Run independent scenarios in parallel; serialize only an explicitly shared external resource.

## Raw ProjectUnit Fast Path

`ProjectUnitFixture<TUnit>` is a raw collaboration harness. It builds a small Microsoft DI provider, activates the target with `ActivatorUtilities`, and assigns Monica's cached service provider when supported.

Use it only when every collaborator is explicit and the test does not depend on module composition, type discovery, conventional registration, dynamic proxies, interceptors, options binding, hosted lifecycle, or host isolation.

There is no separate application-service fixture. Application services use `ProjectUnitFixture<TUnit>` for honest raw collaboration tests or a full host scenario when Monica runtime behavior matters.

## Shared Infrastructure

- `Hosting/` — host factory, scenario application and scope, seam replacement, database isolation, deterministic logging and HTTP support
- `ProjectUnits/` — raw ProjectUnit fixture and execution helpers
- `Repository/` — direct DbContext and repository helpers
- `Doubles/` — deterministic state, user, audit, and event boundaries
- `Results/` — explicit assertions for `Res`, `Res<T>`, and `ResPaged<T>`
- `Localization/` and `ObjectMapping/` — focused shared test support

UI-specific support lives in `Monica.Testing.UI` so non-UI tests do not acquire Monica UI or bUnit dependencies through the core toolkit.

## Validation

Run under WSL with a Windows path and keep one CLI test process active at a time:

```bash
dotnet test 'D:\Code\MoLibrary\Monica.slnx' --results-directory 'D:\Code\MoLibrary\tests\TestResults'
```
