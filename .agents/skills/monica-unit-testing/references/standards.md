# Monica Testing Standards

Use this reference when you need the stable, Monica-specific testing rules without re-reading the full skill.

## Layout

- All unit-test-related artifacts live under `tests/`.
- Shared infrastructure goes in root `Monica.UnitTests/`.
- Runnable test projects use the `Test.Monica.*` prefix.
- Test folders should mirror the source project layout.

## Core Rules

- Prefer testing the public surface before opening internals.
- Facade tests must assert `Res` or `Res<T>` explicitly.
- `Res<string>` success paths must assert `Data`.
- Module tests should cover guide methods, dependencies, or service registration behavior.
- Sociable application tests should use `MonicaApplicationFixture<TStartupModule>` when a module startup type exists.
- Business application sociable tests should follow the `monica-application-unit-testing` skill and use one collection fixture per service test project.
- UI unit tests stop at component and page-shell scope.
- Avoid real network, real persistence, and real browser automation in unit tests.

## Stack

- `xUnit v3`
- `AwesomeAssertions`
- `NSubstitute`
- `coverlet.collector`
- `bUnit` only for UI test projects

## Shared Helpers

- `ResultAssertionExtensions`
- `ModuleTestScope`
- `EchoStringLocalizer<T>`
- `TestThemeState`
- `MonicaApplicationFixture<TStartupModule>`
- `ApplicationServiceFixture<THandler>`
- `DbContextFixture<TDbContext>`

## Application Service Tests

- Default to `MonicaApplicationFixture<TStartupModule>` when a startup module exists.
- Use `ApplicationServiceFixture<THandler>` only for narrow fast-path tests where all collaborators are deliberately substituted.
- Resolve application services, domain services, and repositories from the test scope instead of constructing them directly.

## WSL Execution

- Use Windows paths for `dotnet build` and `dotnet test`.
- Use one build or test process at a time.
