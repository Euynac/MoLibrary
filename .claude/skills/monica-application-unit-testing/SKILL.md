---
name: monica-application-unit-testing
description: Create, migrate, or review sociable tests for Monica-based business services. Use for Test.{ProductionProjectName} architecture, MonicaTestApplicationFactory host-owned scenarios, handler/domain-service/repository/module tests, pre-build seam replacement, database isolation, raw ProjectUnit fast paths, parallel test isolation, or migration from mock-heavy legacy fixtures.
---

# Monica Application Unit Testing

Test business behavior through a complete Monica host when composition matters. Each scenario owns its host; registrations are finalized before build, and scopes only provide normal scoped lifetimes.

## Workflow

1. Inspect the production startup path, module guides, discovery assemblies, DbContexts, and external adapters.
2. Create one runnable project named `Test.{ProductionProjectName}` for the exact production project stem.
3. Add a project-level factory derived from `MonicaTestApplicationFactory<TDiscoveryAnchor>`:
   - Override `ConfigureMonica(IMonicaBuilder)` with the production module graph required by the service.
   - Override `TypeDiscoveryAssemblies` when production ProjectUnits span more than the anchor assembly.
   - Override `ConfigureHost(WebApplicationBuilder)` only for test host configuration or environment inputs.
   - Override `ConfigureServices(IServiceCollection)` for stable test providers and boundary seams used by every scenario; call the base implementation first to retain Monica's standard seams.
4. In every sociable test, call `CreateAsync(...)` to build a complete host for that scenario.
5. Supply scenario-specific registrations through the optional `Action<ISeamReplacementBuilder>` callback to `CreateAsync`. The callback runs before host build.
6. Call `application.CreateScope(...)`, resolve the unit from the concrete `MonicaTestScope`, and pass its cancellation token to async operations.
7. Assert public behavior and observable side effects, then dispose the scope and application.
8. Run the target test project with a Windows path under WSL.

## Factory Contract

`MonicaTestApplicationFactory<TDiscoveryAnchor>` is a reusable composition recipe, not a shared host.

- `ConfigureHost(WebApplicationBuilder)` configures the future host.
- `TypeDiscoveryAssemblies` selects the production assemblies scanned for ProjectUnits; it contains the anchor assembly by default.
- `ConfigureMonica(IMonicaBuilder)` defines the real Monica composition and is required.
- `ConfigureServices(IServiceCollection)` applies stable test registrations before build.
- `CreateAsync(Action<ISeamReplacementBuilder>? configureScenario = null, CancellationToken cancellationToken = default)` creates and starts a new full host.

The resulting `MonicaTestApplication` exposes `Services`, `Application`, `ModuleSnapshots`, and `CreateScope(CancellationToken)`. `CreateScope` never changes service registrations. Use another `CreateAsync` call when a test needs a different registration graph.

## Boundary Choice

Use a full scenario host for application services, domain services, repositories, module registration, options, mapping, interceptors, events, jobs, and behavior spanning scopes.

Use raw `ProjectUnitFixture<TUnit>` only for a narrow collaboration test where every dependency is explicit and Monica composition is irrelevant. It does not validate discovery, conventional registration, dynamic proxies, module options, hosted lifecycle, or host ownership. Do not use or recreate `ApplicationServiceFixture<THandler>`.

Entity invariant tests and deterministic value-object tests may construct objects directly.

## Required Conventions

- Project, folder, assembly, and root namespace: `Test.{ProductionProjectName}`
- Project factory: `{Service}TestApplicationFactory`
- Test class: `{TypeUnderTest}Tests`
- Test method: `Method_WhenCondition_ShouldExpectation`
- Host-backed test field: `_factory`
- Source-aligned folders: `HandlersCommand`, `HandlersQuery`, `DomainServices`, `Repositories`, `Entities`, and `Modules`
- Test-only support folders: `Factories`, `Builders`, `TestDoubles`, and `TestData`

Do not put all service tests in one xUnit collection. Independent scenario hosts run in parallel by default. Use a named collection only when tests intentionally share a real external resource that cannot be isolated, and document that resource.

## Hard Rules

- Replace boundaries, not domain logic.
- Resolve production handlers, domain services, repositories, mappers, and options from the scenario host.
- Apply registration overrides before host build; never copy descriptors from a built provider or replace services while creating a scope.
- Never share a `MonicaApplication` between root providers.
- Keep stateful doubles owned by one scenario host or its scopes.
- Use unique names or explicit serialization for external databases, ports, files, topics, and queues.
- Avoid real network, uncontrolled external databases, sleeps, random/manual output, and hidden machine dependencies.
- Keep assertions in tests rather than setup helpers.

## References

- Read `references/standards.md` for project, ownership, and migration rules.
- Read `references/templates.md` for exact factory and scenario shapes.
- Read `references/database-isolation.md` before selecting a database strategy.

## Validation

- Run `dotnet test 'D:\Path\To\Solution\src\Tests\Test.{ProductionProjectName}\Test.{ProductionProjectName}.csproj'`.
- Use one `dotnet build` or `dotnet test` process at a time.
- Treat warnings introduced by the touched test project as failures.
