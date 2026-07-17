# Monica Testing Standards

## Layout

- Keep runnable tests under `tests/`.
- Put shared test infrastructure in `Monica.Testing/`.
- Name runnable framework projects `Test.Monica.*` and mirror the source layout inside them.

## Boundary Selection

| Behavior under test | Test boundary |
|---|---|
| Value objects, parsers, validators, deterministic support code | Direct construction |
| One ProjectUnit with every collaborator supplied explicitly | Raw `ProjectUnitFixture<TUnit>` |
| Module graph, options, DI registration, proxies, hosted lifecycle, or cross-scope behavior | `MonicaTestApplicationFactory<TDiscoveryAnchor>` scenario |
| Blazor component or page shell | bUnit in the UI test project |

`ProjectUnitFixture<TUnit>` does not validate Monica composition. Do not add a separate `ApplicationServiceFixture`; application services use either the raw ProjectUnit fast path or a real scenario host according to the behavior being tested.

## Scenario Ownership

- Treat one `MonicaTestApplicationFactory.CreateAsync(...)` call as one test scenario.
- Give every scenario its own complete host and `MonicaApplication`.
- Apply module and service registrations before host build.
- Apply stable project seams in factory `ConfigureServices(...)`.
- Apply test-specific seams in the `CreateAsync(...)` callback.
- Create child scopes with `MonicaTestApplication.CreateScope(...)`; never use scopes to replace registrations.
- Dispose scopes before disposing their application.
- Never copy a built host's service descriptors into a second root provider.

## Isolation and Parallelism

- Run independent scenarios in parallel by default; host-owned Monica state does not require global serialization.
- Keep stateful doubles owned by the scenario host or a scenario scope.
- Use unique database names, ports, directories, topics, and queue names when a test touches external state.
- Serialize only a named shared external resource that cannot be isolated. Document the reason on that collection or fixture.
- Add contract coverage for two simultaneous hosts when changing module composition, logging ownership, type discovery, or singleton lifetimes.

## Core Assertions

- Prefer public-surface assertions.
- Assert `Res` and `Res<T>` status, message, and data explicitly.
- Assert successful `Res<string>.Data` to catch the string-overload trap.
- Test module guides through dependencies, options, registrations, or runtime snapshots.
- Assert side effects through repositories, DbContexts, recording event buses, state stores, or other public boundaries.

## Stack

- xUnit v3
- AwesomeAssertions
- NSubstitute and `NSubstitute.Analyzers.CSharp`
- coverlet.collector
- bUnit only in UI test projects

## WSL Execution

- Use Windows paths for `dotnet build` and `dotnet test`.
- Run a single build or test process at a time; use MSBuild's internal parallelism instead of concurrent CLI processes.
