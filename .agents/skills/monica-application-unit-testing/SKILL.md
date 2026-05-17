---
name: monica-application-unit-testing
description: Use when creating, migrating, or reviewing sociable unit tests for Monica-based application or business services, including Test.{Service} project architecture, MonicaApplicationFixture collection fixtures, per-scope seam replacement, DbContext isolation, handler/domain-service/repository tests, and migration from mock-heavy legacy tests.
---

# Monica Application Unit Testing

Use sociable application tests for Monica-based business services. Boot the real Monica module graph once per service test project, replace only external seams, and resolve handlers, domain services, repositories, and module registrations from DI.

## When To Use

- Create a `Test.{Service}` project for an application service.
- Migrate old NUnit/xUnit2/Moq/scratch tests into the new `Test.*` style.
- Test command handlers, query handlers, domain services, repositories, and module/service registration in a Monica application.
- Decide whether a test should use `MonicaApplicationFixture<TStartupModule>` or the fast-path `ApplicationServiceFixture<THandler>`.
- Choose a database isolation strategy for application tests.

## Workflow

1. Read the service's startup path first. Identify its module startup type or service runner and the DbContexts it registers.
2. Create one runnable test project per service: `src/Tests/Test.{Service}` for business solutions or `tests/Test.Monica.{Project}` for Monica framework projects.
3. Add `CollectionFixtures/{Service}Collection.cs` and `{Service}TestFixture.cs`. The fixture should derive from `MonicaApplicationFixture<TStartupModule>` when the service has a startup module.
4. Override fixture defaults only for boundaries:
   - test databases
   - distributed state
   - event bus
   - HTTP/RPC clients
   - current user or tenant context
5. Keep test folders aligned with production folders: `HandlersCommand`, `HandlersQuery`, `DomainServices`, `Repositories`, `Entities`, and `Modules`.
6. In each sociable test, start with `await using var scope = _app.NewScope(...)`, resolve the unit under test from the scope, pass `scope.CancellationToken`, and assert public behavior plus observable side effects.
7. Run the target test project with Windows paths under WSL. Run a single `dotnet test` process at a time.

## Default Choice

Prefer `MonicaApplicationFixture<TStartupModule>` for application services because it catches real DI and module-wiring failures. Use `ApplicationServiceFixture<THandler>` only for narrow fast-path handler tests where all collaborators are intentionally substituted and module boot would add noise.

## Required Conventions

- Test project: `Test.{Service}` for business services, `Test.Monica.{Project}` for Monica framework projects.
- Collection class: `{Service}Collection` with a public `Name` constant.
- Fixture class: `{Service}TestFixture`.
- Test class: `{TypeUnderTest}Tests`.
- Test method: `Method_WhenCondition_ShouldExpectation`.
- Field name for fixture in test classes: `_app`.
- Every sociable test class uses `[Collection({Service}Collection.Name)]`.
- Entity invariant tests may instantiate entities directly and do not need a collection.

## Hard Rules

- Replace boundaries, not domain logic.
- Do not use real network, real external databases, sleeps, or browser automation in unit tests.
- Do not mock constructors just to satisfy DI. Let the service provider build the real unit under test.
- Do not use abstract `TestBase` classes for new tests. Use fixture composition.
- Do not hide assertions in setup helpers. Builders can create data; tests must state the behavior being verified.
- Keep xUnit collection parallelism disabled for Monica application tests unless Monica module state has been proven isolated for parallel host boots.

## Read As Needed

- `references/standards.md`
  - Stable architecture and migration rules.
- `references/templates.md`
  - Copyable collection, fixture, handler, repository, domain-service, module, and entity test skeletons.
- `references/database-isolation.md`
  - Choosing between per-scope SQLite, shared SQLite with transaction rollback, and real provider-backed database tests.

## Validation

- `dotnet test 'D:\Path\To\Solution\src\Tests\Test.{Service}\Test.{Service}.csproj'`
- Run with `--logger "console;verbosity=detailed"` when checking fixture boot time.
- Treat warnings from the touched test project as failures. Existing application warnings may be documented separately when they are outside the migration scope.
