# Sociable Application Test Templates

These templates use `UserService.API` as a neutral example. Keep the real runnable project name equal to `Test.` plus the exact production project stem.

## Project Factory

```csharp
public sealed class UserServiceTestApplicationFactory
    : MonicaTestApplicationFactory<CommandHandlerUserLogin>
{
    protected override void ConfigureHost(WebApplicationBuilder builder)
    {
        builder.Environment.EnvironmentName = Environments.Development;
    }

    protected override void ConfigureMonica(IMonicaBuilder monica)
    {
        monica.AddUserService(options => options.EnableExternalNotifications = false);
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        services.UseTestDatabase<UserDbContext>(DatabaseIsolation.PerScopeDatabase);
        services.RemoveAll<IExternalUserDirectory>();
        services.AddSingleton<IExternalUserDirectory, StubExternalUserDirectory>();
    }
}
```

The factory is a stateless recipe. `CreateAsync(...)` builds a new host; do not cache an application or provider on the factory.

## Command Handler Scenario

```csharp
public sealed class CommandHandlerUserLoginTests(
    UserServiceTestApplicationFactory factory)
    : IClassFixture<UserServiceTestApplicationFactory>
{
    private readonly UserServiceTestApplicationFactory _factory = factory;

    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ShouldIssueTokenWithBusinessClaims()
    {
        var jwt = Substitute.For<IJwtAuthManager>();
        var expectedToken = CreateJwtAuthResult("exam01");
        Claim[] issuedClaims = [];

        jwt.GenerateTokens("exam01", Arg.Do<Claim[]>(claims => issuedClaims = claims), Arg.Any<DateTime?>())
            .Returns(expectedToken);

        await using var application = await _factory.CreateAsync(
            scenario => scenario.With<IJwtAuthManager>(jwt),
            TestContext.Current.CancellationToken);
        await using var scope = application.CreateScope(TestContext.Current.CancellationToken);
        await SeedLoginUserAsync(scope);

        var handler = scope.Resolve<CommandHandlerUserLogin>();
        var result = await handler.Handle(
            new CommandUserLogin
            {
                Username = "exam01",
                Password = "pass123",
                GrantType = EGrantType.PasswordPlain
            },
            scope.CancellationToken);

        var data = result.ShouldSucceed();
        data!.AccessToken.Should().Be(expectedToken.AccessToken);
        issuedClaims.Should().Contain(claim =>
            claim.Type == AuthorityClaimTypes.Username && claim.Value == "exam01");
        jwt.Received(1).GenerateTokens("exam01", Arg.Any<Claim[]>(), Arg.Any<DateTime?>());
    }
}
```

The replacement callback changes the service collection before `Build()`. `CreateScope()` only creates a child scope.

## Repository Scenario

```csharp
public sealed class RepositoryUserTests(
    UserServiceTestApplicationFactory factory)
    : IClassFixture<UserServiceTestApplicationFactory>
{
    private readonly UserServiceTestApplicationFactory _factory = factory;

    [Fact]
    public async Task GetUserInfo_WhenUserExists_ShouldReturnUserWithOrganUnit()
    {
        await using var application = await _factory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await using var scope = application.CreateScope(TestContext.Current.CancellationToken);
        await scope.SeedAsync(
            new OrganUnit { Id = 20, OrganName = "Test Tower", Code = "ZBAA-TWR" },
            new User
            {
                Id = Guid.NewGuid(),
                Username = "exam01",
                Nickname = "Exam User",
                OrganUnitId = 20
            });

        var repository = scope.Resolve<IRepositoryUser>();
        var user = await repository.GetUserInfo("exam01");

        user.Should().NotBeNull();
        user!.OrganUnit.Should().NotBeNull();
    }
}
```

## Module Composition Scenario

```csharp
public sealed class UserServiceModuleTests(
    UserServiceTestApplicationFactory factory)
    : IClassFixture<UserServiceTestApplicationFactory>
{
    private readonly UserServiceTestApplicationFactory _factory = factory;

    [Fact]
    public async Task Module_WhenHostStarts_ShouldExposeExpectedComposition()
    {
        await using var application = await _factory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        application.Application.Should().BeSameAs(
            application.Services.GetRequiredService<MonicaApplication>());
        application.ModuleSnapshots.Should().Contain(snapshot =>
            snapshot.ModuleType == typeof(ModuleUserService));
        application.Services.GetService<IRepositoryUser>().Should().NotBeNull();
    }
}
```

## Raw ProjectUnit Fast Path

```csharp
public sealed class QueryHandlerUserCheckTests
{
    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnBadRequest()
    {
        await using var fixture = ProjectUnitFixture<QueryHandlerUserCheck>
            .Builder()
            .WithSubstitute<IRepositoryUser>(out var repository)
            .Build();

        repository.GetUserInfo("missing").Returns(Task.FromResult<User?>(null));

        var result = await fixture.Unit.Handle(
            new QueryUserCheck { Username = "missing" },
            CancellationToken.None);

        result.ShouldFail(ResStatus.BadRequest, "user does not exist");
        result.Data.Should().BeNull();
    }
}
```

This is raw Microsoft DI activation. Use it only when module registration, options, proxies, interceptors, hosted lifecycle, and host isolation are outside the assertion.

## Entity Invariant

```csharp
public sealed class UserTests
{
    [Fact]
    public void Create_WhenRequiredValuesAreValid_ShouldPreserveIdentity()
    {
        var user = User.Create("exam01", "Exam User");

        user.Username.Should().Be("exam01");
        user.Nickname.Should().Be("Exam User");
    }
}
```
