# Sociable Application Testing Standards

## Test Shape

A sociable scenario creates a complete Monica host, replaces only boundaries before host build, and resolves production handlers, domain services, repositories, mappers, options, and module services from DI.

Use focused direct or raw ProjectUnit tests only when host composition adds no relevant behavior.

## Project Layout

Name the runnable project `Test.` plus the exact production project stem. Keep its folder, project file, assembly, and root namespace identical.

```text
Test.UserService.API/
  Test.UserService.API.csproj
  GlobalUsings.cs
  Factories/
    UserServiceTestApplicationFactory.cs
  HandlersCommand/
  HandlersQuery/
  DomainServices/
  Repositories/
  Entities/
  Modules/
  Builders/
  TestDoubles/
  TestData/
```

Do not shorten suffixes such as `.API`, `.Domain`, `.Infrastructure`, `.Adaptor`, or `.WebAPI`.

## Factory Rules

- Derive one stateless project recipe from `MonicaTestApplicationFactory<TDiscoveryAnchor>`.
- Choose an anchor whose assembly contains the business types that Monica must discover.
- Override `TypeDiscoveryAssemblies` when the scenario spans multiple production assemblies; do not duplicate Monica type-discovery setup inside `ConfigureMonica`.
- Implement `ConfigureMonica(IMonicaBuilder)` with the relevant production module graph.
- Use `ConfigureHost(WebApplicationBuilder)` for configuration sources, environment, or other host-builder inputs.
- Use `ConfigureServices(IServiceCollection)` for stable test databases and boundary adapters shared by all scenarios; call `base.ConfigureServices(services)` first unless all standard seams are intentionally replaced.
- Keep project-specific double behavior in `TestDoubles/`.
- Never store a created `MonicaTestApplication`, service provider, scope, or mutable scenario state on the factory.

## Scenario Rules

- Call `factory.CreateAsync(...)` once per independently isolated scenario.
- Put test-specific seam registrations in its `ISeamReplacementBuilder` callback.
- Create normal child scopes with `application.CreateScope(...)`.
- Resolve units under test from `MonicaTestScope`.
- Dispose every scope before its owning application.
- Use multiple scopes in one application only when the behavior deliberately spans scopes under the same host.
- Use `application.Services`, `application.Application`, or `application.ModuleSnapshots` for host-level assertions.

The service collection is immutable after build. A scope may select scoped state, but it cannot replace registrations.

## Replacement Rules

Replace leaves and adapters:

- state stores and caches
- event transports
- DbContext provider or database
- HTTP/RPC clients
- current user or tenant context
- local configuration adapters
- clocks, random sources, or ID generators when nondeterminism matters

Do not replace application services, domain services, repositories, or mappers unless that type is itself the external boundary under test.

## Fast Path

Use `ProjectUnitFixture<TUnit>` only as a raw activation harness with explicit collaborators. Its assertions must not claim to cover Monica module wiring, type discovery, conventional registration, dynamic proxies, interceptors, options binding, hosted lifecycle, or host isolation.

Do not maintain a parallel `ApplicationServiceFixture<THandler>` abstraction.

## Migration Rules

- Replace copied service-descriptor roots and shared `MonicaApplication` fixtures with scenario hosts.
- Move per-scope registration replacements to the `CreateAsync(...)` callback.
- Replace shared mutable singleton doubles with scenario-owned registrations.
- Delete compatibility fixtures that manually reconstruct production DI.
- Delete tests with no assertions, manual output only, uncontrolled random loops, untracked files, or live external calls.
- Convert retained tests to xUnit v3, NSubstitute, and AwesomeAssertions.

## Parallelism

Run independent scenarios in parallel because each scenario host owns its composition state.

Serialize only tests that share a named external resource that cannot be isolated. Prefer unique database names, ports, directories, topics, queue names, and containers before introducing a collection-level lock.
