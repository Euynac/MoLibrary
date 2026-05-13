using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Metrics;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Test.Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services;

public class ConfigurationMutationServiceTests
{
    [Fact]
    public async Task MutateAsync_WhenTargetSourceKeyIsProvided_ShouldWriteToThatSource()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var target = new RecordingValueSource("memory:target", 100);
        var other = new RecordingValueSource("memory:other", 300);
        var service = CreateService([target, other]);

        await service.MutateAsync(Request(targetSourceKey: "memory:target"), cancellationToken);

        target.Mutations.Should().ContainSingle();
        other.Mutations.Should().BeEmpty();
    }

    [Fact]
    public async Task MutateAsync_WhenTargetSourceKeyIsMissing_ShouldWriteToHighestPriorityWritableSource()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var low = new RecordingValueSource("memory:low", 100);
        var high = new RecordingValueSource("memory:high", 300);
        var readOnly = new RecordingValueSource("memory:readonly", 500, isWritable: false);
        var service = CreateService([low, high, readOnly]);

        await service.MutateAsync(Request(), cancellationToken);

        high.Mutations.Should().ContainSingle();
        low.Mutations.Should().BeEmpty();
        readOnly.Mutations.Should().BeEmpty();
    }

    [Fact]
    public async Task MutateAsync_WhenWriteSucceeds_ShouldReloadAfterSourceWrite()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var events = new List<string>();
        var source = new RecordingValueSource("memory:target", 100, events);
        var reload = new RecordingReloadCoordinator(events);
        var service = CreateService([source], reloadCoordinator: reload);

        await service.MutateAsync(Request(), cancellationToken);

        events.Should().Equal("mutate:memory:target", "reload");
    }

    [Fact]
    public async Task MutateAsync_WhenWriteSucceeds_ShouldBroadcastNotificationToAllBroadcasters()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new RecordingValueSource("memory:target", 100);
        var firstBroadcaster = new RecordingBroadcaster();
        var secondBroadcaster = new RecordingBroadcaster();
        var service = CreateService([source], broadcasters: [firstBroadcaster, secondBroadcaster]);

        var result = await service.MutateAsync(Request(), cancellationToken);

        result.NewVersion.Should().Be(1);
        firstBroadcaster.Notifications.Should().ContainSingle();
        secondBroadcaster.Notifications.Should().ContainSingle();
        firstBroadcaster.Notifications[0].DefinitionKey.Should().Be(TestConfigurationFactory.DefinitionKey);
        firstBroadcaster.Notifications[0].SourceKey.Should().Be("memory:target");
        firstBroadcaster.Notifications[0].LogicalPath.Should().Be(LogicalPath.FromProperties("WorkerId"));
        firstBroadcaster.Notifications[0].Version.Should().Be(1);
    }

    private static ConfigurationMutationService CreateService(
        IReadOnlyList<IConfigurationValueSource> sources,
        RecordingReloadCoordinator? reloadCoordinator = null,
        IReadOnlyList<IConfigurationChangeBroadcaster>? broadcasters = null)
    {
        var registry = new ConfigurationDefinitionRegistry();
        registry.Register(TestConfigurationFactory.Definition());

        return new ConfigurationMutationService(
            registry,
            sources,
            new ConfigurationValidationCoordinator(),
            new PassThroughSensitiveValueProtector(),
            new ConfigurationPathProjector(),
            reloadCoordinator ?? new RecordingReloadCoordinator(),
            broadcasters ?? [],
            new ConfigurationMetricsRecorder(new RecordingMeterFactory()));
    }

    [Fact]
    public async Task MutateAsync_WhenLogicalPathUsesListIndex_ShouldRejectMutation()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var source = new RecordingValueSource("memory:target", 100);
        var service = CreateService([source]);
        var request = Request(logicalPath: new LogicalPath(
        [
            new PropertySegment("Services"),
            new ListIndexSegment(0),
            new PropertySegment("Name")
        ]));

        var act = () => service.MutateAsync(request, cancellationToken);

        await act.Should().ThrowAsync<ConfigurationValidationFailedException>();
    }

    private static ConfigurationMutationRequest Request(string? targetSourceKey = null, LogicalPath? logicalPath = null)
    {
        return new ConfigurationMutationRequest
        {
            DefinitionKey = TestConfigurationFactory.DefinitionKey,
            LogicalPath = logicalPath ?? LogicalPath.FromProperties("WorkerId"),
            MutationKind = ConfigurationMutationKind.Set,
            Value = ConfigurationStoredValue.Plain("12"),
            TargetSourceKey = targetSourceKey,
            Context = new ConfigurationMutationContext
            {
                ModifierId = "tester",
                ModifierName = "Tester"
            }
        };
    }

    private sealed class RecordingValueSource(
        string sourceKey,
        int priority,
        List<string>? events = null,
        bool isWritable = true)
        : IConfigurationValueSource
    {
        public List<ConfigurationSourceMutation> Mutations { get; } = [];

        public ConfigurationSourceDescriptor Descriptor { get; } = new()
        {
            SourceKey = sourceKey,
            DisplayName = sourceKey,
            Kind = ConfigurationSourceKind.Memory,
            Priority = priority,
            IsWritable = isWritable
        };

        public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>([]);
        }

        public Task<ConfigurationValueOverride?> GetAsync(
            string definitionKey,
            LogicalPath logicalPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ConfigurationValueOverride?>(null);
        }

        public Task<ConfigurationMutationResult> MutateAsync(
            ConfigurationSourceMutation mutation,
            CancellationToken cancellationToken)
        {
            events?.Add($"mutate:{Descriptor.SourceKey}");
            Mutations.Add(mutation);
            return Task.FromResult(new ConfigurationMutationResult
            {
                DefinitionKey = mutation.Request.DefinitionKey,
                LogicalPath = mutation.Request.LogicalPath,
                NewVersion = 1,
                SchemaVersion = mutation.Definition.SchemaVersion,
                ModifiedTime = DateTimeOffset.UtcNow
            });
        }
    }

    private sealed class RecordingReloadCoordinator(List<string>? events = null) : IConfigurationReloadCoordinator
    {
        public int ReloadCount { get; private set; }

        public Task ReloadAsync(CancellationToken cancellationToken)
        {
            events?.Add("reload");
            ReloadCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBroadcaster : IConfigurationChangeBroadcaster
    {
        public List<ConfigurationChangeNotification> Notifications { get; } = [];

        public Task BroadcastAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options)
        {
            return new Meter(options);
        }

        public void Dispose()
        {
        }
    }
}
