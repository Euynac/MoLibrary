# Sociable Application Test Templates

## Collection

```csharp
namespace Test.AlarmService.CollectionFixtures;

[CollectionDefinition(Name)]
public sealed class AlarmServiceCollection : ICollectionFixture<AlarmServiceTestFixture>
{
    public const string Name = "AlarmService";
}
```

## Fixture

```csharp
namespace Test.AlarmService.CollectionFixtures;

public sealed class AlarmServiceTestFixture : MonicaApplicationFixture<ModuleAlarmService>
{
    protected override void ConfigureDefaults(IServiceCollection services)
    {
        base.ConfigureDefaults(services);
        services.UseTestDatabase<AlarmDbContext>(DatabaseIsolation.PerScopeDatabase);
        services.UseTestDatabase<AlarmHistoryDbContext>(DatabaseIsolation.PerScopeDatabase);
    }

    protected override void ConfigureModule(IHostApplicationBuilder builder)
    {
        new ModuleAlarmServiceGuide()
            .Register(options =>
            {
                options.ProjectName = "Test.AlarmService";
            });
    }
}
```

If the business service has no Monica startup module, keep the sample on `ApplicationServiceFixture<THandler>` or introduce a business compatibility fixture that knows how to run that service's existing runner without external infrastructure.

## Handler Test

```csharp
[Collection(AlarmServiceCollection.Name)]
public sealed class CommandHandlerAriseAlarmFlightTests(AlarmServiceTestFixture app)
{
    private readonly AlarmServiceTestFixture _app = app;

    [Fact]
    public async Task Handle_WhenAlarmDoesNotExist_ShouldInsertAndPublishFlight()
    {
        await using var scope = _app.NewScope(replace => replace
            .Substitute<IDistributedStateStore>(out var stateStore));

        stateStore.GetStateAsync<List<DtoAlarmType>>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([new DtoAlarmType { Category = "FLT", AlarmCode = "001", IsSound = true }]);

        var handler = scope.Resolve<CommandHandlerAriseAlarmFlight>();
        var result = await handler.Handle(new CommandAriseAlarmFlight(), scope.CancellationToken);

        var data = result.ShouldSucceed();
        data.AFID.Should().NotBeNullOrWhiteSpace();
    }
}
```

## Repository Test

```csharp
[Collection(AlarmServiceCollection.Name)]
public sealed class RepositoryAlarmFlightTests(AlarmServiceTestFixture app)
{
    private readonly AlarmServiceTestFixture _app = app;

    [Fact]
    public async Task GetListAsync_WhenSeededWithMatchingRows_ShouldReturnMatches()
    {
        await using var scope = _app.NewScope();
        await scope.SeedAsync(
            AlarmFlightBuilder.Default().WithFpid(100).Build(),
            AlarmFlightBuilder.Default().WithFpid(100).Build(),
            AlarmFlightBuilder.Default().WithFpid(200).Build());

        var repository = scope.Resolve<IRepositoryAlarmFlight>();
        var matches = await repository.GetListAsync(a => a.FPID == 100, scope.CancellationToken);

        matches.Should().HaveCount(2);
    }
}
```

## Domain Service Test

```csharp
[Collection(AlarmServiceCollection.Name)]
public sealed class DomainPublishFlightTests(AlarmServiceTestFixture app)
{
    private readonly AlarmServiceTestFixture _app = app;

    [Fact]
    public async Task PublishFlight_WhenInputProvided_ShouldPublishAlarmFlightEvent()
    {
        await using var scope = _app.NewScope();

        var service = scope.Resolve<DomainPublishFlight>();
        var eventBus = scope.Resolve<RecordingEventBus>();

        await service.PublishFlight([AlarmFlightDtoBuilder.Default().Build()], unProcess: true);

        eventBus.Recorded<EventAlarmFlightEto>().Should().HaveCount(1);
    }
}
```

## Module Registration Test

```csharp
[Collection(AlarmServiceCollection.Name)]
public sealed class AlarmServiceModuleTests(AlarmServiceTestFixture app)
{
    private readonly AlarmServiceTestFixture _app = app;

    [Fact]
    public void Module_WhenBooted_ShouldRegisterExpectedServices()
    {
        _app.Services.GetService<IRepositoryAlarmFlight>().Should().NotBeNull();
        _app.Services.GetService<DomainPublishFlight>().Should().NotBeNull();
    }
}
```

## Entity Invariant Test

```csharp
public sealed class AlarmFlightTests
{
    [Fact]
    public void AutoSetNewId_WhenIdIsZero_ShouldAssignId()
    {
        var alarm = new AlarmFlight();
        alarm.AutoSetNewId();
        alarm.Id.Should().NotBe(0);
    }
}
```
