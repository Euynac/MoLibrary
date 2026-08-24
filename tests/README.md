# Monica Testing

This directory contains Monica's runnable test projects and shared execution conventions. The testing model separates direct logic tests, raw ProjectUnit collaboration tests, complete host-owned scenarios, and Blazor component tests so each assertion uses the correct runtime boundary.

## Scope

- `Monica.Testing` provides host-owned scenario infrastructure, raw ProjectUnit fixtures, result assertions, repository helpers, and deterministic boundary doubles.
- `Monica.Testing.UI` provides reusable UI test support without adding UI dependencies to the core toolkit.
- `tests/Test.Monica.*` contains runnable framework test projects.
- Real external integration, browser automation, and end-to-end suites are outside this unit-test layer.

## Choosing a Test Boundary

| Behavior | Boundary |
|---|---|
| Value object, parser, validator, or deterministic support logic | Construct directly |
| One ProjectUnit with explicit collaborators | Raw `ProjectUnitFixture<TUnit>` |
| Module graph, options, registration, hosted lifecycle, or cross-scope behavior | Full `MonicaTestApplicationFactory<TDiscoveryAnchor>` scenario |
| Blazor component or page shell | bUnit in a UI test project |
| Source-generator input, diagnostics, generated source, or same-compilation binding | In-memory Roslyn `GeneratorDriver` |

`ProjectUnitFixture<TUnit>` uses a small raw Microsoft DI container. It does not prove Monica type discovery, conventional registration, module options, hosted lifecycle, or host ownership. There is no separate `ApplicationServiceFixture<THandler>`; apply the same boundary decision to application services as to every other ProjectUnit.

## Host-Owned Scenario Model

Create a reusable project factory:

```csharp
public sealed class JobSchedulerTestApplicationFactory
    : MonicaTestApplicationFactory<TestRecurringJob>
{
    protected override void ConfigureMonica(IMonicaBuilder monica)
    {
        monica.AddJobScheduler(options => options.ProjectName = "Test.Monica.JobScheduler")
            .AsStandalone()
            .UseInMemoryStore()
            .UseSchedulerScope("job-tests")
            .UseCatalogRelease(
                "job-tests:scenario-1",
                deploymentGeneration: 1,
                [new("Test.Monica.JobScheduler", "job-tests:scenario-1")])
            .UseLocalWorkerIdentity("Test.Monica.JobScheduler", "job-tests:scenario-1");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        // Register stable test providers shared by every scenario from this factory.
    }
}
```

Create one complete application per test scenario:

```csharp
var externalClient = Substitute.For<IExternalClient>();

await using var application = await _factory.CreateAsync(
    scenario => scenario.With<IExternalClient>(externalClient),
    TestContext.Current.CancellationToken);
await using var scope = application.CreateScope(TestContext.Current.CancellationToken);

var service = scope.Resolve<MyApplicationService>();
var result = await service.ExecuteAsync(scope.CancellationToken);
```

`CreateAsync(...)` builds and starts a distinct host with its own `MonicaApplication`, module graph, singleton services, and disposal boundary. The optional scenario callback changes the service collection before build. `CreateScope(...)` creates a normal child scope and never replaces registrations.

Use `ConfigureHost(WebApplicationBuilder)` for test environment or configuration inputs, `ConfigureMonica(IMonicaBuilder)` for the real module composition, and `ConfigureServices(IServiceCollection)` for stable test boundaries. Use another `CreateAsync(...)` call when registrations differ between scenarios.

## Isolation and Parallelism

- Independent scenarios run in parallel by default because Monica composition is host-owned.
- Keep stateful doubles, databases, files, ports, topics, queues, and IDs owned or uniquely named by a scenario.
- Use xUnit collections only for a named external resource that cannot be isolated. Document the resource and why serialization is required.
- Dispose every `MonicaTestScope` before its `MonicaTestApplication`.
- Never copy descriptors from a built provider, build a second root around an existing `MonicaApplication`, or mutate registrations while creating a scope.
- Do not start multiple independent `dotnet build` or `dotnet test` processes in WSL. Test-runner parallelism inside one process remains allowed.

## Directory and Naming Rules

- Shared core toolkit: `Monica.Testing/Monica.Testing.csproj`
- Shared UI toolkit: `Monica.Testing.UI/Monica.Testing.UI.csproj`
- Runnable framework project: `tests/Test.Monica.{ProjectName}/`
- Test class: `{TypeName}Tests`
- Test method: `Method_WhenCondition_ShouldExpectation`
- Project factory: `{ProjectName}TestApplicationFactory`

Mirror source folders such as `Modules/`, `Facades/`, `Services/`, `Providers/`, `Pages/`, and `Components/`. Keep scenario-only builders, doubles, and data in clearly named support folders within the runnable project.

## Stack

- xUnit v3
- AwesomeAssertions
- NSubstitute with `NSubstitute.Analyzers.CSharp`
- coverlet.collector
- bUnit only in UI test projects

## Monica-Specific Assertions

- Prefer public-surface behavior.
- Assert facade `Res` or `Res<T>` status, message, and data explicitly.
- Always assert successful `Res<string>.Data` to catch the `Res.Ok(string)` overload trap.
- Test module registration extensions through dependencies, options, DI-visible registrations, or runtime snapshots rather than static tables alone.
- Avoid real network, uncontrolled persistence, sleeps, random/manual output, and hidden developer-machine state.

## Adding a Runnable Test Project

1. Create `tests/Test.Monica.{ProjectName}` and mirror the production project name.
2. Reference `Monica.Testing` and the production project.
3. Reference `Monica.Testing.UI` and bUnit only for UI tests.
4. Add a `MonicaTestApplicationFactory<TDiscoveryAnchor>` only when host composition is part of the behavior.
5. Add direct or raw ProjectUnit tests where the smaller boundary is honest.
6. Keep scenario-specific seams in the runnable project; promote only genuinely reusable infrastructure to the shared toolkit.

Source-generator tests are the exception to the host and ProjectUnit boundaries above. Keep them in a dedicated `Test.Monica.{GeneratorProject}` project, instantiate the incremental generator directly, and run it against an in-memory `CSharpCompilation`. Assert the updated compilation as well as generated text so same-compilation references are proven. Repeated identical runs must remain deterministic and must not create repository files; reserve solution-build tests for behavior owned by analyzer packaging or MSBuild targets.

## WSL Execution

Use Windows paths:

```bash
dotnet test 'D:\Code\MoLibrary\Monica.slnx' --results-directory 'D:\Code\MoLibrary\tests\TestResults'
```

For coverage:

```bash
dotnet test 'D:\Code\MoLibrary\Monica.slnx' --collect:"XPlat Code Coverage" --results-directory 'D:\Code\MoLibrary\tests\TestResults'
```
