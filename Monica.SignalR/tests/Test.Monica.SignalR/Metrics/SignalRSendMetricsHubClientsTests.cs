using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Models;
using Monica.Modules;
using Monica.SignalR.Metrics;
using Monica.SignalR.Models;
using Monica.SignalR.Services.Support;
using AwesomeAssertions;
using Xunit;

namespace Test.Monica.SignalR.Metrics;

internal interface ITestHubContract
{
    Task ReceiveTestMessage(string user, string message);
}

public sealed class SignalRSendMetricsHubClientsTests
{
    [Fact]
    public async Task User_WhenTargetUserIsOnline_ShouldCaptureUsernameAsDisplayName()
    {
        var userId = Guid.NewGuid().ToString();
        var (clients, metrics) = CreateClients(RegisterOnlineUser(userId, "alice"), includeTargetIdentifiers: true);

        await clients.User(userId).ReceiveTestMessage("a", "b");

        var row = GetSingleMetricRow(metrics);
        row.TargetIdentifiers.Should().ContainSingle().Which.Should().Be(userId);
        row.TargetIdentifierDisplayNames.Should().ContainSingle().Which.Should().Be("alice");
    }

    [Fact]
    public async Task User_WhenTargetUserIsOffline_ShouldFallBackToEmptyDisplayName()
    {
        var (clients, metrics) = CreateClients(new SignalRConnectionRegistry(), includeTargetIdentifiers: true);
        var offlineUserId = Guid.NewGuid().ToString();

        await clients.User(offlineUserId).ReceiveTestMessage("a", "b");

        var row = GetSingleMetricRow(metrics);
        row.TargetIdentifierDisplayNames.Should().ContainSingle().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task Users_WhenTargetsMixOnlineAndOfflineUsers_ShouldAlignDisplayNamesWithIdentifiers()
    {
        var onlineUserId = Guid.NewGuid().ToString();
        var (clients, metrics) = CreateClients(RegisterOnlineUser(onlineUserId, "bob"), includeTargetIdentifiers: true);
        var offlineUserId = Guid.NewGuid().ToString();

        await clients.Users([onlineUserId, offlineUserId]).ReceiveTestMessage("a", "b");

        var row = GetSingleMetricRow(metrics);
        row.TargetIdentifiers.Should().Equal([onlineUserId, offlineUserId]);
        row.TargetIdentifierDisplayNames.Should().Equal(["bob", string.Empty]);
    }

    [Fact]
    public async Task User_WhenIdentifierCaptureIsDisabled_ShouldNotRetainIdentifiersOrDisplayNames()
    {
        var userId = Guid.NewGuid().ToString();
        var (clients, metrics) = CreateClients(RegisterOnlineUser(userId, "alice"), includeTargetIdentifiers: false);

        await clients.User(userId).ReceiveTestMessage("a", "b");

        var row = GetSingleMetricRow(metrics);
        row.TargetIdentifiers.Should().BeEmpty();
        row.TargetIdentifierDisplayNames.Should().BeEmpty();
        row.TargetCount.Should().Be(1);
    }

    private static (SignalRSendMetricsHubClients<ITestHubContract> Clients, SignalRSendMetrics Metrics) CreateClients(
        SignalRConnectionRegistry registry,
        bool includeTargetIdentifiers)
    {
        var metrics = new SignalRSendMetrics(
            new TestMeterFactory(),
            Options.Create(new ModuleSignalROption
            {
                EnableSendMetrics = true,
                IncludeSendDiagnosticTargetIdentifiers = true
            }));

        var clients = new SignalRSendMetricsHubClients<ITestHubContract>(
            new TestHubClients(),
            metrics,
            "TestHub",
            includeTargetIdentifiers,
            registry);

        return (clients, metrics);
    }

    private static SignalRSendMetricInfo GetSingleMetricRow(SignalRSendMetrics metrics)
    {
        return metrics.GetSnapshot().Metrics.Single();
    }

    private static SignalRConnectionRegistry RegisterOnlineUser(string userId, string username)
    {
        var registry = new SignalRConnectionRegistry();
        registry.AddConnection($"conn-{username}", new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AuthorityClaimTypes.Username, username),
            new Claim(AuthorityClaimTypes.UserId, userId)
        ], "TestAuth")));
        return registry;
    }

    private sealed class TestHubClients : IHubClients<ITestHubContract>
    {
        public ITestHubContract All => new TestHubContract();

        public ITestHubContract AllExcept(IReadOnlyList<string> excludedConnectionIds) => new TestHubContract();

        public ITestHubContract Client(string connectionId) => new TestHubContract();

        public ITestHubContract Clients(IReadOnlyList<string> connectionIds) => new TestHubContract();

        public ITestHubContract Group(string groupName) => new TestHubContract();

        public ITestHubContract Groups(IReadOnlyList<string> groupNames) => new TestHubContract();

        public ITestHubContract GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new TestHubContract();

        public ITestHubContract User(string userId) => new TestHubContract();

        public ITestHubContract Users(IReadOnlyList<string> userIds) => new TestHubContract();
    }

    private sealed class TestHubContract : ITestHubContract
    {
        public Task ReceiveTestMessage(string user, string message) => Task.CompletedTask;
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public void Dispose()
        {
        }

        Meter IMeterFactory.Create(MeterOptions options) => new(options.Name);
    }
}
