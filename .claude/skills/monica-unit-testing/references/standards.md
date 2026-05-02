# Monica Testing Standards

Use this reference when you need the stable, Monica-specific testing rules without re-reading the full skill.

## Layout

- All unit-test-related artifacts live under `tests/`.
- Shared infrastructure goes in `tests/Test.Monica/`.
- Runnable test projects use the `Test.Monica.*` prefix.
- Test folders should mirror the source project layout.

## Core Rules

- Prefer testing the public surface before opening internals.
- Facade tests must assert `Res` or `Res<T>` explicitly.
- `Res<string>` success paths must assert `Data`.
- Module tests should cover guide methods, dependencies, or service registration behavior.
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

## WSL Execution

- Use Windows paths for `dotnet build` and `dotnet test`.
- Use one build or test process at a time.
