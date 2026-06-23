using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Modules;

namespace Monica.Configuration.Services;

/// <summary>
/// Receives remote configuration reload signals and applies locally relevant Monica projection reloads.
/// </summary>
internal sealed class ConfigurationReloadSignalReceiver(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationReloadCoordinator reloadCoordinator,
    IOptions<ModuleConfigurationOption> options)
    : IConfigurationReloadSignalReceiver
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenNotificationIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long?> _pendingVersionsByDefinition = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _scheduleLock = new();
    private Task? _scheduledFlush;

    /// <inheritdoc />
    public Task ReceiveAsync(ConfigurationChangeNotification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ShouldReceive(notification))
        {
            return Task.CompletedTask;
        }

        if (notification.Scope == ConfigurationReloadScope.RuntimeConfiguration)
        {
            return Task.CompletedTask;
        }

        _pendingVersionsByDefinition.AddOrUpdate(
            notification.DefinitionKey,
            notification.Version,
            (_, existing) => NewerVersion(existing, notification.Version));
        ScheduleFlush();
        return Task.CompletedTask;
    }

    private bool ShouldReceive(ConfigurationChangeNotification notification)
    {
        var currentOptions = options.Value;
        PruneSeenNotifications(currentOptions);

        if (string.IsNullOrWhiteSpace(notification.NotificationId)
            || !_seenNotificationIds.TryAdd(notification.NotificationId, DateTimeOffset.UtcNow))
        {
            return false;
        }

        if (string.Equals(notification.OriginInstanceId, currentOptions.InstanceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!definitionRegistry.TryGet(notification.DefinitionKey, out _))
        {
            return false;
        }

        if (notification.Scope == ConfigurationReloadScope.MonicaProjection
            && notification.Version is { } version
            && reloadCoordinator.GetLoadedMonicaProjectionVersion(notification.DefinitionKey) is { } loadedVersion
            && loadedVersion >= version)
        {
            return false;
        }

        return notification.Scope is ConfigurationReloadScope.MonicaProjection or ConfigurationReloadScope.RuntimeConfiguration;
    }

    private void ScheduleFlush()
    {
        lock (_scheduleLock)
        {
            if (_scheduledFlush is { IsCompleted: false })
            {
                return;
            }

            _scheduledFlush = FlushAfterDelayAsync();
        }
    }

    private async Task FlushAfterDelayAsync()
    {
        var currentOptions = options.Value;
        var debounceDelay = NonNegative(currentOptions.RemoteReloadDebounceDelay);
        if (debounceDelay > TimeSpan.Zero)
        {
            await Task.Delay(debounceDelay);
        }

        var jitterDelay = RandomJitter(NonNegative(currentOptions.RemoteReloadMaxJitterDelay));
        if (jitterDelay > TimeSpan.Zero)
        {
            await Task.Delay(jitterDelay);
        }

        var pending = DrainPendingReloads();
        foreach (var (definitionKey, version) in pending.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            await reloadCoordinator.ReloadMonicaProjectionAsync(definitionKey, version, CancellationToken.None);
        }
    }

    private Dictionary<string, long?> DrainPendingReloads()
    {
        var pending = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (definitionKey, version) in _pendingVersionsByDefinition)
        {
            if (_pendingVersionsByDefinition.TryRemove(definitionKey, out var removedVersion))
            {
                pending[definitionKey] = removedVersion ?? version;
            }
        }

        return pending;
    }

    private void PruneSeenNotifications(ModuleConfigurationOption currentOptions)
    {
        var window = NonNegative(currentOptions.RemoteReloadDedupeWindow);
        if (window == TimeSpan.Zero)
        {
            _seenNotificationIds.Clear();
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - window;
        foreach (var (notificationId, seenAt) in _seenNotificationIds)
        {
            if (seenAt < cutoff)
            {
                _seenNotificationIds.TryRemove(notificationId, out _);
            }
        }
    }

    private static long? NewerVersion(long? left, long? right)
    {
        return left is null || right is null ? left ?? right : Math.Max(left.Value, right.Value);
    }

    private static TimeSpan RandomJitter(TimeSpan maxDelay)
    {
        if (maxDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var maxMilliseconds = (int)Math.Min(maxDelay.TotalMilliseconds, int.MaxValue - 1);
        return TimeSpan.FromMilliseconds(Random.Shared.Next(maxMilliseconds + 1));
    }

    private static TimeSpan NonNegative(TimeSpan value)
    {
        return value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }
}
