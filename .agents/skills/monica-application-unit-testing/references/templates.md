# Sociable Application Test Templates

These templates use `UserService.API` as the neutral business-service sample. When adapting them, keep the runnable test project name equal to `Test.` plus the exact production project name.

## Collection

```csharp
namespace Test.UserService.API.CollectionFixtures;

[CollectionDefinition(Name)]
public sealed class UserServiceCollection : ICollectionFixture<UserServiceTestFixture>
{
    public const string Name = "UserService";
}
```

## Fixture

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Test.UserService.API.CollectionFixtures;

public sealed class UserServiceTestFixture : PlatformApplicationFixture<CommandHandlerUserLogin>
{
    protected override void ConfigureMappings(TypeAdapterConfig config)
    {
        config.NewConfig<JwtAuthResult, ResponseUserLogin>();
        config.NewConfig<User, ResponseUserCheck>()
            .Map(destination => destination.OrganUnit, source => source.OrganUnit);
        config.NewConfig<OrganUnit, DtoOrganUnit>();
    }

    protected override void ConfigureService(IServiceCollection services)
    {
        AddTestDbContext<UserDbContext>(services);
        AddRepository<IRepositoryUser, RepositoryUser>(services);
        AddRepository<IRepositoryRole, RepositoryRole>(services);
        AddRepository<IRepositoryPermission, RepositoryPermission>(services);

        services.AddScoped<DomainUserManager>();
        services.AddScoped<QueryHandlerUserCheck>();
        services.AddScoped<CommandHandlerUserLogin>();
        services.AddSingleton<IPasswordCrypto, PasswordCrypto>();
        services.AddSingleton<IAeroLocalConfig, TestAeroLocalConfig>();
        services.AddSingleton<ISnowflakeIdGenerator, SequentialTestSnowflakeIdGenerator>();

        services.RemoveAll<IJwtAuthManager>();
        services.AddSingleton<IJwtAuthManager, StubJwtAuthManager>();
    }
}
```

If the business service has a Monica startup module, derive from `MonicaApplicationFixture<TStartupModule>` and override the module guide/database options. If it has no startup module or the host is too broad for the scenario, use a business compatibility fixture such as `PlatformApplicationFixture<TService>` and register the real collaborators needed by the unit under test.

## Command Handler Test

```csharp
[Collection(UserServiceCollection.Name)]
public sealed class CommandHandlerUserLoginTests(UserServiceTestFixture app)
{
    private readonly UserServiceTestFixture _app = app;

    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ShouldIssueTokenWithBusinessClaims()
    {
        await using var scope = _app.NewScope(replace => replace.Substitute<IJwtAuthManager>(out _));
        var jwt = scope.Resolve<IJwtAuthManager>();
        var expectedToken = CreateJwtAuthResult("exam01");
        Claim[] issuedClaims = [];

        jwt.GenerateTokens("exam01", Arg.Do<Claim[]>(claims => issuedClaims = claims), Arg.Any<DateTime?>())
            .Returns(expectedToken);
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
        issuedClaims.Should().Contain(claim => claim.Type == AuthorityClaimTypes.Username && claim.Value == "exam01");
        jwt.Received(1).GenerateTokens("exam01", Arg.Any<Claim[]>(), Arg.Any<DateTime?>());
    }
}
```

## Query Handler Fast-Path Test

```csharp
public sealed class QueryHandlerUserCheckTests
{
    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnBadRequest()
    {
        const string username = "missing";
        using var fixture = PlatformApplicationServiceFixture
            .Builder<QueryHandlerUserCheck>()
            .WithSubstitute<IRepositoryUser>(out var repository)
            .Build();

        repository.GetUserInfo(username).Returns(Task.FromResult<User?>(null));

        var result = await fixture.Service.Handle(
            new QueryUserCheck { Username = username },
            CancellationToken.None);

        result.ShouldFail(ResStatus.BadRequest, "user does not exist");
        result.Data.Should().BeNull();
    }
}
```

## Repository Test

```csharp
[Collection(UserServiceCollection.Name)]
public sealed class RepositoryUserTests(UserServiceTestFixture app)
{
    private readonly UserServiceTestFixture _app = app;

    [Fact]
    public async Task GetUserInfo_WhenUserExists_ShouldReturnUserWithOrganUnit()
    {
        await using var scope = _app.NewScope();
        await scope.SeedAsync(
            new OrganUnit { Id = 20, OrganName = "Test Tower", Code = "ZBAA-TWR" },
            new User { Id = Guid.NewGuid(), Username = "exam01", Nickname = "Exam User", OrganUnitId = 20 });

        var repository = scope.Resolve<IRepositoryUser>();
        var user = await repository.GetUserInfo("exam01");

        user.Should().NotBeNull();
        user!.OrganUnit.Should().NotBeNull();
    }
}
```

## Module Registration Test

```csharp
[Collection(UserServiceCollection.Name)]
public sealed class UserServiceModuleTests(UserServiceTestFixture app)
{
    private readonly UserServiceTestFixture _app = app;

    [Fact]
    public void Module_WhenBooted_ShouldRegisterExpectedServices()
    {
        _app.Services.GetService<IRepositoryUser>().Should().NotBeNull();
        _app.Services.GetService<CommandHandlerUserLogin>().Should().NotBeNull();
        _app.Services.GetService<IPasswordCrypto>().Should().NotBeNull();
    }
}
```

## Entity Invariant Test

```csharp
public sealed class UserTests
{
    [Fact]
    public void IsActive_WhenNewUserCreated_ShouldDefaultToExpectedState()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "exam01",
            Nickname = "Exam User"
        };

        user.Username.Should().Be("exam01");
        user.Nickname.Should().Be("Exam User");
    }
}
```
