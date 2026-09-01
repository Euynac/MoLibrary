using System.Collections.Concurrent;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;

namespace Monica.EventBus.Services;

/// <summary>
/// Default in-memory <see cref="ITopicSubscriptionStatusStore"/>.
/// One mutable entry per (serviceKey, topicName) guarded by its own lock; reads and change
/// notifications hand out immutable <see cref="TopicSubscriptionStatus"/> snapshots.
/// </summary>
public class TopicSubscriptionStatusStore : ITopicSubscriptionStatusStore
{
    /// <summary>Maximum number of entries retained per topic in the recent-error history.</summary>
    private const int RECENT_ERROR_LIMIT = 10;

    private sealed class Entry
    {
        public required object Lock { get; init; }
        public required MutableStatus Status { get; init; }

        // Hot-path message counters live outside the entry lock: updated with Interlocked on
        // every handled message, read only when a snapshot is produced.
        public long ProcessedMessages;
        public long LastMessageTicks;
    }

    private readonly ConcurrentDictionary<(string? ServiceKey, string TopicName), Entry> _entries = new();

    public event Action<TopicSubscriptionStatus>? StatusChanged;

    public void ReportState(
        string? serviceKey,
        string topicName,
        EventBusProviderKind provider,
        TopicSubscriptionRuntimeState state,
        string? providerMessage = null,
        Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        var snapshot = Mutate(serviceKey, topicName, entry =>
        {
            var status = entry.Status;
            status.Provider = provider;
            status.State = state;
            status.StateChangedAt = DateTime.UtcNow;

            if (state == TopicSubscriptionRuntimeState.Recovering)
            {
                status.ConsecutiveFailures++;
                status.RecoveryCount++;
            }
            else if (state == TopicSubscriptionRuntimeState.Healthy)
            {
                status.ConsecutiveFailures = 0;
            }

            // A transition message with an exception is a first-class error record; a plain
            // transition message (for example "recovery scheduled in 00:00:05") is context only.
            if (exception is not null)
            {
                RecordError(status, providerMessage, exception);
            }
        });

        RaiseStatusChanged(snapshot);
    }

    public void ReportError(string? serviceKey, string topicName, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var snapshot = Mutate(serviceKey, topicName, entry =>
        {
            RecordError(entry.Status, message, exception);
        });

        RaiseStatusChanged(snapshot);
    }

    public void ReportMessageProcessed(string? serviceKey, string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);

        // Deliberately no lock, no snapshot, and no StatusChanged notification: this runs per
        // handled message and must stay allocation-free so high-throughput topics never cause
        // change-notification storms.
        var entry = GetOrCreateEntry(serviceKey, topicName);
        Interlocked.Increment(ref entry.ProcessedMessages);
        Volatile.Write(ref entry.LastMessageTicks, DateTime.UtcNow.Ticks);
    }

    public IReadOnlyList<TopicSubscriptionStatus> GetAll()
    {
        return _entries.Values
            .Select(entry => Snapshot(entry))
            .ToList();
    }

    public TopicSubscriptionStatus? Get(string? serviceKey, string topicName)
    {
        return _entries.TryGetValue((serviceKey, topicName), out var entry) ? Snapshot(entry) : null;
    }

    /// <summary>
    /// Runs <paramref name="mutate"/> under the entry lock after ensuring the entry exists,
    /// and returns the post-mutation snapshot. New entries start in
    /// <see cref="TopicSubscriptionRuntimeState.Subscribing"/> because the first report for a
    /// topic always happens while its external subscription is being established.
    /// </summary>
    private TopicSubscriptionStatus Mutate(
        string? serviceKey,
        string topicName,
        Action<Entry> mutate)
    {
        var entry = GetOrCreateEntry(serviceKey, topicName);

        lock (entry.Lock)
        {
            mutate(entry);
            return Snapshot(entry);
        }
    }

    private Entry GetOrCreateEntry(string? serviceKey, string topicName)
    {
        return _entries.GetOrAdd(
            (serviceKey, topicName),
            static key => new Entry
            {
                Lock = new object(),
                Status = new MutableStatus
                {
                    ServiceKey = key.ServiceKey,
                    TopicName = key.TopicName,
                    State = TopicSubscriptionRuntimeState.Subscribing,
                    StateChangedAt = DateTime.UtcNow,
                    ConsecutiveFailures = 0,
                    RecoveryCount = 0,
                    MessageErrorCount = 0,
                    RecentErrors = []
                }
            });
    }

    private static void RecordError(MutableStatus status, string? message, Exception? exception)
    {
        var occurredAt = DateTime.UtcNow;
        var errorText = message ?? exception?.Message;
        if (errorText is null)
        {
            return;
        }

        status.LastErrorAt = occurredAt;
        status.LastErrorMessage = errorText;
        status.LastErrorException = exception?.ToString();
        status.MessageErrorCount++;

        var recent = status.RecentErrors.ToList();
        recent.Add(new TopicSubscriptionError(occurredAt, errorText, exception?.ToString()));
        if (recent.Count > RECENT_ERROR_LIMIT)
        {
            recent.RemoveRange(0, recent.Count - RECENT_ERROR_LIMIT);
        }

        status.RecentErrors = recent;
    }

    private static TopicSubscriptionStatus Snapshot(Entry entry)
    {
        var status = entry.Status;
        var lastMessageTicks = Volatile.Read(ref entry.LastMessageTicks);
        return new TopicSubscriptionStatus
        {
            ServiceKey = status.ServiceKey,
            TopicName = status.TopicName,
            Provider = status.Provider,
            State = status.State,
            StateChangedAt = status.StateChangedAt,
            LastErrorAt = status.LastErrorAt,
            LastErrorMessage = status.LastErrorMessage,
            LastErrorException = status.LastErrorException,
            ConsecutiveFailures = status.ConsecutiveFailures,
            RecoveryCount = status.RecoveryCount,
            MessageErrorCount = status.MessageErrorCount,
            ProcessedMessages = Interlocked.Read(ref entry.ProcessedMessages),
            LastMessageReceivedAt = lastMessageTicks == 0
                ? null
                : new DateTime(lastMessageTicks, DateTimeKind.Utc),
            RecentErrors = status.RecentErrors
        };
    }

    private void RaiseStatusChanged(TopicSubscriptionStatus snapshot)
    {
        var handlers = StatusChanged;
        if (handlers is null)
        {
            return;
        }

        // Notifications fire outside the entry lock; a throwing consumer must never
        // break provider reporting.
        foreach (var handler in handlers.GetInvocationList().Cast<Action<TopicSubscriptionStatus>>())
        {
            try
            {
                handler(snapshot);
            }
            catch
            {
                // Intentionally ignored: consumers are UI/monitoring side effects.
            }
        }
    }

    /// <summary>In-place mutable fields for one topic; guarded by the owning entry lock.</summary>
    private sealed class MutableStatus
    {
        public required string? ServiceKey { get; init; }
        public required string TopicName { get; init; }
        public EventBusProviderKind Provider { get; set; } = EventBusProviderKind.Unknown;
        public required TopicSubscriptionRuntimeState State { get; set; }
        public required DateTime StateChangedAt { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public string? LastErrorMessage { get; set; }
        public string? LastErrorException { get; set; }
        public int ConsecutiveFailures { get; set; }
        public long RecoveryCount { get; set; }
        public long MessageErrorCount { get; set; }
        public IReadOnlyList<TopicSubscriptionError> RecentErrors { get; set; } = [];
    }
}
