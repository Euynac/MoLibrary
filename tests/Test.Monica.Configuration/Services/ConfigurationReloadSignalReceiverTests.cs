using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Services;

public sealed class ConfigurationReloadSignalReceiverTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ReceiveAsync_WhenNotificationIsRelevant_ShouldDebounceAndReloadDefinitionOnce()
    {
        var definition = TestConfigurationFactory.Definition();
        var registry = new ConfigurationDefinitionRegistry();
        registry.Register(definition);
        var coordinator = new RecordingReloadCoordinator();
        var receiver = CreateReceiver(registry, coordinator);
        var first = CreateSignal(definition.DefinitionKey, version: 2);
        var duplicateDefinition = CreateSignal(definition.DefinitionKey, version: 3);

        await receiver.ReceiveAsync(first, CancellationToken.None);
        await receiver.ReceiveAsync(duplicateDefinition, CancellationToken.None);

        await coordinator.WaitForReloadAsync(definition.DefinitionKey, minimumVersion: 3)
            .WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
        var reload = coordinator.Reloads.Should().ContainSingle().Subject;
        reload.DefinitionKey.Should().Be(definition.DefinitionKey);
        reload.MinimumVersion.Should().Be(3);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNotificationIsIgnored_ShouldNotReload()
    {
        var definition = TestConfigurationFactory.Definition();
        var registry = new ConfigurationDefinitionRegistry();
        registry.Register(definition);
        var coordinator = new RecordingReloadCoordinator();
        coordinator.SetLoadedVersion(definition.DefinitionKey, 5);
        var receiver = CreateReceiver(registry, coordinator);

        await receiver.ReceiveAsync(CreateSignal("Unknown.Definition", version: 1), CancellationToken.None);
        await receiver.ReceiveAsync(CreateSignal(definition.DefinitionKey, version: 4), CancellationToken.None);
        await receiver.ReceiveAsync(CreateSignal(definition.DefinitionKey, version: 6) with
        {
            OriginInstanceId = "local-instance"
        }, CancellationToken.None);
        await receiver.ReceiveAsync(CreateSignal(definition.DefinitionKey, version: 7) with
        {
            Kind = (ConfigurationReloadSignalKind)999
        }, CancellationToken.None);
        await Task.Delay(80, TestContext.Current.CancellationToken);

        coordinator.Reloads.Should().BeEmpty();
    }

    private static ConfigurationReloadSignalReceiver CreateReceiver(
        ConfigurationDefinitionRegistry registry,
        RecordingReloadCoordinator coordinator)
    {
        var metadataStore = Substitute.For<IConfigurationMetadataStore>();
        var definitionResolver = new ConfigurationDefinitionResolver(registry, metadataStore);
        return new ConfigurationReloadSignalReceiver(
            registry,
            definitionResolver,
            coordinator,
            Options.Create(new ModuleConfigurationOption
            {
                InstanceId = "local-instance",
                RemoteReloadDebounceDelay = TimeSpan.FromMilliseconds(10),
                RemoteReloadMaxJitterDelay = TimeSpan.Zero,
                RemoteReloadDedupeWindow = TimeSpan.FromMinutes(1)
            }));
    }

    private static ConfigurationReloadSignal CreateSignal(string definitionKey, long version)
    {
        return new ConfigurationReloadSignal
        {
            SignalId = Guid.NewGuid().ToString("N"),
            OriginInstanceId = "remote-instance",
            StoreKey = "file:default",
            Kind = ConfigurationReloadSignalKind.DefinitionsChanged,
            Definitions =
            [
                new ConfigurationReloadDefinitionVersion
                {
                    DefinitionKey = definitionKey,
                    Version = version
                }
            ],
            ChangedTime = DateTimeOffset.UtcNow
        };
    }

    private sealed class RecordingReloadCoordinator : IConfigurationReloadCoordinator
    {
        private readonly Dictionary<(string DefinitionKey, long? MinimumVersion), TaskCompletionSource>
            _reloadSignals = [];
        private readonly Dictionary<string, long?> _loadedVersions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string DefinitionKey, long? MinimumVersion)> _reloads = [];
        private readonly object _lock = new();

        public IReadOnlyList<(string DefinitionKey, long? MinimumVersion)> Reloads
        {
            get
            {
                lock (_lock)
                {
                    return [.. _reloads];
                }
            }
        }

        public Task ReloadMonicaProjectionAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ReloadMonicaProjectionAsync(string definitionKey, long? minimumVersion, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                var reload = (DefinitionKey: definitionKey, MinimumVersion: minimumVersion);
                _reloads.Add(reload);
                _loadedVersions[definitionKey] = minimumVersion;
                if (_reloadSignals.TryGetValue(reload, out var signal))
                {
                    signal.TrySetResult();
                }
            }

            return Task.CompletedTask;
        }

        public Task ReloadRuntimeConfigurationAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public long? GetLoadedMonicaProjectionVersion(string definitionKey)
        {
            lock (_lock)
            {
                return _loadedVersions.GetValueOrDefault(definitionKey);
            }
        }

        public void SetLoadedVersion(string definitionKey, long version)
        {
            lock (_lock)
            {
                _loadedVersions[definitionKey] = version;
            }
        }

        public Task WaitForReloadAsync(string definitionKey, long? minimumVersion)
        {
            lock (_lock)
            {
                var expected = (DefinitionKey: definitionKey, MinimumVersion: minimumVersion);
                if (_reloads.Contains(expected))
                {
                    return Task.CompletedTask;
                }

                if (!_reloadSignals.TryGetValue(expected, out var signal))
                {
                    signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _reloadSignals.Add(expected, signal);
                }

                return signal.Task;
            }
        }
    }
}
