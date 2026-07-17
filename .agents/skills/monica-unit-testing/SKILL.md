---
name: monica-unit-testing
description: Create, migrate, or review Monica framework tests and shared testing infrastructure. Use for tests under tests/, the Monica.Testing toolkit, host-owned MonicaTestApplicationFactory scenarios, raw ProjectUnitFixture tests, xUnit v3 or bUnit setup, module tests, facade result assertions, test isolation, and testing documentation or skills.
---

# Monica Unit Testing

Use `Monica.Testing` as the shared toolkit and keep runnable framework tests under `tests/Test.Monica.*`. Choose the smallest test boundary that proves the behavior without misrepresenting Monica's activation or host lifecycle.

## Workflow

1. Read `<project-root>/tests/README.md` for the current project layout and execution rules.
2. Classify the test before choosing infrastructure:
   - Pure logic: construct the value or service directly.
   - Raw ProjectUnit collaboration: use `ProjectUnitFixture<TUnit>` and accept its activation limits.
   - Module wiring, options, conventional registration, proxies, hosted lifecycle, or cross-scope behavior: create a full host with `MonicaTestApplicationFactory<TDiscoveryAnchor>`.
   - Blazor component or page shell: use bUnit in the runnable UI test project.
3. Put reusable assertions, host helpers, and deterministic boundary doubles in `Monica.Testing`; keep scenario-specific data and doubles in the runnable test project.
4. Prefer public-surface coverage: module guides, facades, public models and abstractions, stable providers, and observable side effects.
5. Run one `dotnet test` process at a time with Windows paths under WSL.

## Host-Owned Scenarios

Use `MonicaTestApplicationFactory<TDiscoveryAnchor>` when the test depends on real Monica composition.

- Derive a project factory and implement `ConfigureMonica(IMonicaBuilder)` with the production module graph needed by the scenario.
- Use `ConfigureHost(WebApplicationBuilder)` for host configuration and environment inputs.
- Use `ConfigureServices(IServiceCollection)` for stable test-boundary registrations shared by every scenario produced by that factory. Call the base implementation first unless the scenario intentionally replaces all standard seams.
- Call `CreateAsync(...)` for each test scenario. Every call builds and starts a complete host with its own `MonicaApplication`, module graph, singleton services, logger ownership, and disposal boundary.
- Pass scenario-specific seam replacements to `CreateAsync(Action<ISeamReplacementBuilder>?)`. The callback changes registrations before the host is built.
- Call `MonicaTestApplication.CreateScope(...)` only to create a normal child scope. A built service provider is immutable; scope creation never replaces registrations.
- Dispose the `MonicaTestApplication` after the scenario, even when several scopes are used within that scenario.

Do not copy service descriptors into another root provider, share one `MonicaApplication` across providers, or emulate per-scope registration replacement.

## Raw ProjectUnit Fast Path

`ProjectUnitFixture<TUnit>` is an explicitly raw fast-path harness. It creates a small Microsoft DI container, activates the target, and initializes Monica's cached-service-provider accessor where applicable.

Use it only when all collaborators are deliberately supplied and the assertion does not depend on:

- Monica module composition or options binding
- conventional type discovery and registration
- dynamic proxies or interceptors
- hosted-service startup and shutdown
- host-owned singleton or logging isolation

Use the host-owned scenario model for any of those behaviors. Do not introduce another application-service-specific fixture; `ApplicationServiceFixture<THandler>` is not part of the testing model.

## Required Conventions

- Shared toolkit: `Monica.Testing/Monica.Testing.csproj`
- Runnable project: `tests/Test.Monica.{ProjectName}/`
- Test class: `{TypeName}Tests`
- Test method: `Method_WhenCondition_ShouldExpectation`
- Test folders mirror source folders such as `Modules/`, `Facades/`, `Providers/`, `Services/`, `Pages/`, and `Components/`.

## Monica-Specific Rules

- Assert `Res<T>` explicitly. Facade tests cover `Status`, `Message`, and `Data`; successful `Res<string>` paths always verify `Data`.
- Test module behavior through guide configuration, dependencies, resulting options, or DI-visible registrations rather than static tables alone.
- Replace external boundaries, not domain logic. Resolve application services, domain services, repositories, mappers, and options from the scenario host.
- Avoid real network, uncontrolled persistence, sleeps, random inputs, and untracked machine state.
- Keep `InternalsVisibleTo` exceptional and scoped to the corresponding runnable test project.
- Run independent test scenarios in parallel by default. Serialize only tests that name and document a genuinely shared external resource.

## Default Stack

- xUnit v3
- AwesomeAssertions
- NSubstitute with its analyzer
- coverlet.collector
- bUnit in UI test projects only

## References

- Read `references/standards.md` for stable boundary and isolation rules.
- Read `references/samples.md` when selecting an existing framework sample.
- Use `../monica-application-unit-testing/SKILL.md` for Monica-based business-service tests.

## Validation

- Use Windows paths for `dotnet build` and `dotnet test` under WSL.
- Run one build or test process at a time.
- Run the relevant runnable test project or solution after changes.
