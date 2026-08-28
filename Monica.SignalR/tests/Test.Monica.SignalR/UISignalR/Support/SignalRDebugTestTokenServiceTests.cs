using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Authority.Authentication.Abstractions;
using Monica.Authority.Identity.Models;
using Monica.Modules;
using Monica.SignalR.UISignalR.Support;
using AwesomeAssertions;
using Xunit;

namespace Test.Monica.SignalR.UISignalR.Support;

public sealed class SignalRDebugTestTokenServiceTests
{
    [Fact]
    public void GenerateTestUserToken_InDevelopmentWithoutOverride_ShouldMintTokenForRandomNonSystemUser()
    {
        var issuer = new RecordingAccessTokenIssuer();
        var service = CreateService(issuer, environmentName: Environments.Development, enableTestUserConnection: null);

        service.IsEnabled.Should().BeTrue();
        var token = service.GenerateTestUserToken();

        token.Username.Should().StartWith("signalr-debug-").And.HaveLength("signalr-debug-".Length + 8);
        token.AccessToken.Should().Be($"token-{token.Username}");

        var claims = issuer.LastCall!.Value.Claims;
        claims.Should().Contain(claim => claim.Type == AuthorityClaimTypes.Username && claim.Value == token.Username);
        claims.Should().Contain(claim => claim.Type == AuthorityClaimTypes.Nickname);

        var userIdClaim = claims.Single(claim => claim.Type == AuthorityClaimTypes.UserId).Value;
        Guid.TryParse(userIdClaim, out var userId).Should().BeTrue();
        userId.ToString().Should().NotStartWith("00000000-0000-0000-0000-");
    }

    [Fact]
    public void GenerateTestUserToken_WhenCalledTwice_ShouldMintDistinctRandomUsers()
    {
        var service = CreateService(new RecordingAccessTokenIssuer(), Environments.Development, null);

        var first = service.GenerateTestUserToken();
        var second = service.GenerateTestUserToken();

        first.Username.Should().NotBe(second.Username);
    }

    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Development", null, true)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, false)]
    public void IsEnabled_ShouldFollowEnvironmentUnlessOverridden(
        string environmentName,
        bool? enableTestUserConnection,
        bool expected)
    {
        var service = CreateService(new RecordingAccessTokenIssuer(), environmentName, enableTestUserConnection);

        service.IsEnabled.Should().Be(expected);
    }

    [Fact]
    public void IsEnabled_WhenAuthenticationModuleIsMissing_ShouldBeFalse()
    {
        var service = CreateService(issuer: null, Environments.Development, null);

        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void GenerateTestUserToken_WhenDisabled_ShouldThrow()
    {
        var service = CreateService(new RecordingAccessTokenIssuer(), Environments.Production, null);

        var action = () => service.GenerateTestUserToken();

        action.Should().Throw<InvalidOperationException>();
    }

    private static SignalRDebugTestTokenService CreateService(
        RecordingAccessTokenIssuer? issuer,
        string environmentName,
        bool? enableTestUserConnection)
    {
        var services = new ServiceCollection();
        if (issuer is not null)
        {
            services.AddSingleton<IAccessTokenIssuer>(issuer);
        }

        var option = new ModuleSignalRUIOption { EnableTestUserConnection = enableTestUserConnection };
        return new SignalRDebugTestTokenService(
            services.BuildServiceProvider(),
            Options.Create(option),
            new TestHostEnvironment(environmentName));
    }

    private sealed class RecordingAccessTokenIssuer : IAccessTokenIssuer
    {
        public (string Username, Claim[] Claims)? LastCall { get; private set; }

        public string GenerateTokens(string username, Claim[] claims, DateTime? now = null)
        {
            LastCall = (username, claims);
            return $"token-{username}";
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Test.Monica.SignalR";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
