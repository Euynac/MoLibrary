using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Abstractions;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.SignalR.Abstractions;
using Monica.SignalR.Metrics;
using Xunit;

namespace Test.Monica.SignalR.Modules;

public sealed class ModuleSignalRJsonProtocolTests
{
    [Fact]
    public async Task AddSignalR_WhenTwoRegistrationsConfigureJsonProtocol_ShouldComposeBeforeSealing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJsonSerialization(options =>
                options.ConfigureSerializer(static serializerOptions => serializerOptions.MaxDepth = 17));

            var signalR = monica.AddSignalR();
            signalR.AddSignalR<
                ISignalRHubOperator<IFirstClientContract, ICurrentUser>,
                TestHubOperator<IFirstClientContract>,
                IFirstClientContract,
                ICurrentUser>(jsonConfigure: static options =>
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            signalR.AddSignalR<
                ISignalRHubOperator<ISecondClientContract, ICurrentUser>,
                TestHubOperator<ISecondClientContract>,
                ISecondClientContract,
                ICurrentUser>(jsonConfigure: static options =>
                    options.PayloadSerializerOptions.WriteIndented = true);
        });

        await using var application = builder.Build();
        var payloadOptions = application.Services
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value
            .PayloadSerializerOptions;

        payloadOptions.MaxDepth.Should().Be(17);
        payloadOptions.PropertyNamingPolicy.Should().BeSameAs(JsonNamingPolicy.SnakeCaseLower);
        payloadOptions.WriteIndented.Should().BeTrue();
        payloadOptions.IsReadOnly.Should().BeTrue();
    }

    private interface IFirstClientContract : ISignalRHubContract;

    private interface ISecondClientContract : ISignalRHubContract;

    private sealed class TestHub<TContract>(
        ISignalRConnectionRegistry connectionRegistry,
        ILogger<TestHub<TContract>> logger)
        : SignalRHub<TContract>(connectionRegistry, logger)
        where TContract : class, ISignalRHubContract;

    private sealed class TestHubOperator<TContract>(
        IHubContext<TestHub<TContract>, TContract> hubContext,
        ISignalRConnectionRegistry connectionRegistry,
        SignalRSendMetrics sendMetrics)
        : CurrentUserSignalRHubOperator<TContract, TestHub<TContract>>(
            hubContext,
            connectionRegistry,
            sendMetrics)
        where TContract : class, ISignalRHubContract;
}
