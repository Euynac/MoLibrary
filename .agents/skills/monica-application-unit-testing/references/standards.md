# Sociable Application Testing Standards

## Test Shape

Sociable application tests boot a real Monica application graph and replace only boundaries. The test should exercise production registrations for handlers, domain services, repositories, mappers, options, and module guide wiring.

Use this pattern for business application services and DDD modules. Infrastructure modules can still use focused module-guide, provider, facade, or pure service tests when full host boot does not add signal.

## Project Layout

```text
Test.AlarmService/
  Test.AlarmService.csproj
  GlobalUsings.cs
  CollectionFixtures/
    AlarmServiceCollection.cs
    AlarmServiceTestFixture.cs
  HandlersCommand/
    CommandHandlerAriseAlarmFlightTests.cs
  HandlersQuery/
  DomainServices/
  Repositories/
  Entities/
  Modules/
  Builders/
  TestDoubles/
  TestData/
```

Mirror source folders for tests. Keep test-only support folders at the project root.

## Fixture Rules

- One service test project has one primary collection fixture.
- The fixture derives from `MonicaApplicationFixture<TStartupModule>` when a module startup type exists.
- Register test databases in `ConfigureDefaults`.
- Register service-specific module guide options in `ConfigureModule`.
- Use `ConfigureOverrides` only for final replacements after Monica module registration.
- Keep fixtures small; project-specific fake behavior belongs in `TestDoubles/`.

## Test Rules

- Test classes using the host carry `[Collection(ServiceCollection.Name)]`.
- Constructor receives the fixture and stores it as `_app`.
- The first action in each test is `await using var scope = _app.NewScope(...)`.
- Resolve the unit under test from `scope`.
- Pass `scope.CancellationToken` to async Monica or EF calls.
- Assert `Res<T>` with Monica result assertion helpers when applicable.
- Assert side effects through public surfaces: repositories, DbContexts, `RecordingEventBus`, or state-store reads.

## Replacement Rules

Replace leaves and adapters:

- state store
- event bus
- DbContext provider/database
- HTTP/RPC clients
- current user or tenant
- local config/adapters

Do not replace application services, domain services, repositories, or mappers unless the test is deliberately about an external seam exposed through that type.

## Migration Rules

- Delete tests that have no assertions, only `Console.WriteLine`, only performance loops, only random/manual output, or depend on untracked local files.
- Migrate deterministic parser, validator, domain model, and branch behavior tests.
- Convert old Moq/Shouldly/NUnit style to xUnit v3, NSubstitute, and AwesomeAssertions.
- Keep names in `Test.*` form. Do not keep legacy names like `*.Xunit.Test`, `*.XUnit`, or `TestBase`.

## Parallelism

Monica application tests assume serial collection execution because module registration and type discovery include ambient process state. Keep the test runner serial unless the host state isolation has been revalidated.
