using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.Services;

public sealed class ConfigurationReloadSignalReceiverTests
{
    [Fact]
    public async Task ReceiveAsync_WhenNotificationIsRelevant_ShouldDebounceAndReloadDefinitionOnce()
    {
        var definition = TestConfigurationFactory.Definition();
        var registry = new ConfigurationDefinitionRegistry();
        registry.Register(definition);
        var coordinator = new RecordingReloadCoordinator();
        var receiver = CreateReceiver(registry, coordinator);
        var first = CreateNotification(definition.DefinitionKey, version: 2);
        var duplicateDefinition = CreateNotification(definition.DefinitionKey, version: 3);

        await receiver.ReceiveAsync(first, CancellationToken.None);
        await receiver.ReceiveAsync(duplicateDefinition, CancellationToken.None);

        await coordinator.WaitForReloadAsync(definition.DefinitionKey);
        coordinator.ReloadedDefinitions.Should().Equal(definition.DefinitionKey);
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

        await receiver.ReceiveAsync(CreateNotification("Unknown.Definition", version: 1), CancellationToken.None);
        await receiver.ReceiveAsync(CreateNotification(definition.DefinitionKey, version: 4), CancellationToken.None);
        await receiver.ReceiveAsync(CreateNotification(definition.DefinitionKey, version: 6) with
        {
            OriginInstanceId = "local-instance"
        }, CancellationToken.None);
        await receiver.ReceiveAsync(CreateNotification(definition.DefinitionKey, version: 7) with
        {
            Scope = ConfigurationReloadScope.RuntimeConfiguration
        }, CancellationToken.None);
        await Task.Delay(80, TestContext.Current.CancellationToken);

        coordinator.ReloadedDefinitions.Should().BeEmpty();
    }

    private static ConfigurationReloadSignalReceiver CreateReceiver(
        ConfigurationDefinitionRegistry registry,
        RecordingReloadCoordinator coordinator)
    {
        return new ConfigurationReloadSignalReceiver(
            registry,
            coordinator,
            Options.Create(new ModuleConfigurationOption
            {
                InstanceId = "local-instance",
                RemoteReloadDebounceDelay = TimeSpan.FromMilliseconds(10),
                RemoteReloadMaxJitterDelay = TimeSpan.Zero,
                RemoteReloadDedupeWindow = TimeSpan.FromMinutes(1)
            }));
    }

    private static ConfigurationChangeNotification CreateNotification(string definitionKey, long version)
    {
        return new ConfigurationChangeNotification
        {
            NotificationId = Guid.NewGuid().ToString("N"),
            OriginInstanceId = "remote-instance",
            StoreKey = "file:default",
            Scope = ConfigurationReloadScope.MonicaProjection,
            DefinitionKey = definitionKey,
            Version = version,
            ChangedTime = DateTimeOffset.UtcNow
        };
    }

    private sealed class RecordingReloadCoordinator : IConfigurationReloadCoordinator
    {
        private readonly Dictionary<string, TaskCompletionSource> _reloadSignals = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long?> _loadedVersions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _reloadedDefinitions = [];
        private readonly object _lock = new();

        public IReadOnlyList<string> ReloadedDefinitions
        {
            get
            {
                lock (_lock)
                {
                    return [.. _reloadedDefinitions];
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
                _reloadedDefinitions.Add(definitionKey);
                _loadedVersions[definitionKey] = minimumVersion;
                if (_reloadSignals.TryGetValue(definitionKey, out var signal))
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

        public async Task WaitForReloadAsync(string definitionKey)
        {
            TaskCompletionSource signal;
            lock (_lock)
            {
                signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _reloadSignals[definitionKey] = signal;
            }

            await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }
}
