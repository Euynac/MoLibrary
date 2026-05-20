---
name: monica-unit-testing
description: Use when creating, extending, or standardizing Monica unit tests, test project scaffolding, shared test infrastructure, tests/ directory layout, xUnit or bUnit test setup, Monica-specific result assertions, module registration tests, or test-related skills and documentation.
---

# Monica Unit Testing

Create Monica unit tests under `tests/` using the shared root `Monica.UnitTests` toolkit, `Test.Monica.*` runnable project naming, xUnit v3 for unit tests, and bUnit for Blazor UI tests. Follow Monica-specific rules for `Res<T>` assertions, module guide testing, sociable host tests, and UI page or component boundaries instead of inventing per-project test conventions.

## When To Use

- Add a new Monica unit test project.
- Extend or refactor tests under `tests/`.
- Define Monica-wide test rules, test layout, or naming standards.
- Add reusable test helpers to `Monica.UnitTests/`.
- Test module guides, module dependencies, facades returning `Res<T>`, providers, support classes, or Blazor components and pages.
- Create or update a Monica testing skill or testing documentation.

## Workflow

1. Read `<project-root>/tests/README.md` first. It is the current source of truth for Monica test layout, naming, stack, and WSL execution rules.
2. Reuse `Monica.UnitTests/` before adding project-local helpers. Shared result assertions, module reset helpers, localization stubs, UI theme stubs, and sociable test fixtures belong there.
3. Keep all unit-test-related artifacts under `tests/`. New runnable test projects must use the `Test.Monica.*` prefix and mirror the source project folder structure.
4. Prefer public-surface tests first:
   - `Modules/`
   - `Facades/`
   - public models or abstractions
   - stable support classes or in-memory providers
5. For UI modules, stay at component level and page-shell level. Use bUnit, deterministic localizers, and fake or in-memory services. Do not introduce browser automation or real backend dependencies into these unit tests.
6. After edits, run `dotnet test` with Windows paths under WSL. Keep a single build or test process at a time.

## Sociable Application Tests

For Monica-based business applications, prefer the `monica-application-unit-testing` skill. It defines the exact `Test.{ProductionProjectName}` architecture, collection fixtures, database isolation choices, and migration rules for command handlers, query handlers, domain services, repositories, and module-registration tests.

Use `MonicaApplicationFixture<TStartupModule>` as the default when the service has a startup module. Keep `ApplicationServiceFixture<THandler>` as a fast path for narrow, fully substituted handler tests.

## Required Conventions

- Shared test infrastructure project: `Monica.UnitTests/Monica.UnitTests.csproj`
- Runnable test project naming: `tests/Test.Monica.{ProjectName}/`
- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- Test folders should mirror the source layout, for example:
  - `Modules/`
  - `Facades/`
  - `Providers/`
  - `Services/`
  - `Pages/`
  - `Components/`
  - `UIJobScheduler/Shared/Support/`

## Monica-Specific Rules

- Assert `Res<T>` explicitly.
  - Facade tests must assert `Status`, `Message`, and `Data`.
  - For `Res<string>` success paths, always verify `Data`; Monica has a known `Res.Ok(string)` overload trap.
- Module tests should not stop at checking static tables.
  - Prefer testing required config methods, guide methods, dependency declarations, or service registrations.
- Prefer in-memory and deterministic seams.
  - Use in-memory providers, substitutes, or fixed timestamps.
  - Avoid real network, real persistence, and `Task.Delay`-based waiting.
- Use `MonicaApplicationFixture<TStartupModule>` for sociable application tests when the target has a module startup type.
  - Override fixture configuration to register test-friendly providers and replace external seams.
  - Keep the test assertion on public DI-visible behavior instead of private boot internals.
- Keep `InternalsVisibleTo` exceptional.
  - Only add it when public-surface tests genuinely cannot cover critical behavior.
  - Scope it narrowly to the corresponding test project.

## Default Stack

- `xUnit v3`
- `AwesomeAssertions`
- `NSubstitute`
- `NSubstitute.Analyzers.CSharp`
- `coverlet.collector`
- `bUnit` for UI test projects only

## Sample Baseline

- Shared infrastructure: `Monica.UnitTests/`
- UI infrastructure sample: `tests/Test.Monica.UI/`
- Infrastructure sample: `tests/Test.Monica.JobScheduler/`
- UI sample: `tests/Test.Monica.JobScheduler.UI/`

Use these sample projects before introducing a new pattern. When the new work matches an existing sample, copy the sample structure and adapt it instead of inventing another style.

## Read As Needed

- `references/standards.md`
  - Stable Monica testing rules and decision points.
- `references/samples.md`
  - Current sample projects, recommended target types, and starter patterns.
- `../monica-application-unit-testing/SKILL.md`
  - Sociable application testing architecture for Monica-based business services.

## Validation

- Use Windows paths for `dotnet` commands in WSL.
- Run a single `dotnet build` or `dotnet test` process at a time.
- Finish by running the relevant solution or project tests, not only by writing scaffolding.
