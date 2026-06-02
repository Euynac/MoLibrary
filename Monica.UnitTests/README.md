# Monica.UnitTests

`Monica.UnitTests` is the shared testing toolkit for Monica framework projects and Monica-based application services. Runnable tests live in `tests/Test.Monica.*` or business `Test.*` projects; this project contains reusable fixtures, assertions, and deterministic boundary doubles only.

## Primary Pattern: Sociable Application Tests

Use `MonicaApplicationFixture<TStartupModule>` when the target has a Monica startup module. The fixture boots the module graph once for an xUnit collection, applies deterministic test seams, and gives each test an isolated `ITestScope`.

```csharp
await using var scope = _app.NewScope(replace => replace
    .Substitute<IDistributedStateStore>(out var stateStore));

var handler = scope.Resolve<MyCommandHandler>();
var result = await handler.Handle(command, scope.CancellationToken);
```

Prefer replacing external boundaries such as state stores, event buses, DbContexts, current users, and HTTP/RPC clients. Resolve application services, domain services, repositories, and facades from the real DI container.

## Fast Path

`ApplicationServiceFixture<THandler>` is retained for tightly isolated handler tests where all collaborators are intentionally substituted. It is not the default for application-service coverage because it does not validate Monica module wiring.

## Shared Helpers

- `Results/ResultAssertionExtensions.cs` for `Res`, `Res<T>`, and `ResPaged<T>`.
- `Modularity/ModuleTestScope.cs` for isolated module-state tests.
- `Hosting/MonicaApplicationFixture.cs` for sociable host tests.
- `Hosting/ITestScope.cs` for per-test scopes, seeding, and DbContext access.
- `Hosting/DefaultSeams.cs` for default deterministic boundaries.
- `Hosting/DatabaseIsolation.cs` for DbContext test database registration.
- `Repository/DbContextFixture.cs` for direct repository/DbContext tests.
- `Doubles/RecordingEventBus.cs` and `Doubles/InMemoryDistributedStateStore.cs` for observable boundary behavior.

## Validation

Run under WSL with Windows paths:

```bash
dotnet test 'D:\Repositories\WorkTree1\MoLibrary\Monica.slnx' --results-directory 'D:\Repositories\WorkTree1\MoLibrary\tests\TestResults'
```

Use one `dotnet build` or `dotnet test` process at a time.
