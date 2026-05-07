using System.Diagnostics.Metrics;

namespace Monica.Repository.Persistence.Metrics;

/// <summary>
/// Records EF Core connection lifecycle metrics emitted by the Repository persistence feature.
/// </summary>
internal sealed class EfCoreConnectionMetrics
{
    private const string EVENT_TAG_NAME = "event";
    private const string STATE_TAG_NAME = "state";
    private const string EVENT_CREATING = "creating";
    private const string EVENT_CREATED = "created";
    private const string EVENT_CLOSING = "closing";
    private const string EVENT_CLOSED = "closed";
    private const string EVENT_FAILED = "failed";
    private const string EVENT_OPENING = "opening";
    private const string EVENT_OPENED = "opened";
    private const string EVENT_DISPOSING = "disposing";
    private const string EVENT_DISPOSED = "disposed";
    private const string STATE_CREATING = "creating";
    private const string STATE_DISPOSING = "disposing";
    private const string STATE_NOT_CLOSED = "not_closed";

    private readonly Counter<long> _connectionEvents;
    private long _connectionCreatingCount;
    private long _connectionCreatedCount;
    private long _connectionDisposingCount;
    private long _connectionDisposedCount;

    /// <summary>
    /// Initializes Repository EF Core connection instruments.
    /// </summary>
    public EfCoreConnectionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(RepositoryPersistenceMetricNames.MeterName);

        _connectionEvents = meter.CreateCounter<long>(
            RepositoryPersistenceMetricNames.EfCoreConnectionEvents,
            unit: "events",
            description: "EF Core connection lifecycle events observed by Monica Repository.");

        meter.CreateObservableGauge(
            RepositoryPersistenceMetricNames.EfCoreConnectionCurrent,
            ObserveCurrentConnectionStates,
            unit: "connections",
            description: "Current EF Core connection lifecycle state observed by Monica Repository.");
    }

    /// <summary>
    /// Records that EF Core started creating a database connection.
    /// </summary>
    public void RecordConnectionCreating()
    {
        Interlocked.Increment(ref _connectionCreatingCount);
        RecordConnectionEvent(EVENT_CREATING);
    }

    /// <summary>
    /// Records that EF Core created a database connection.
    /// </summary>
    public void RecordConnectionCreated()
    {
        Interlocked.Increment(ref _connectionCreatedCount);
        RecordConnectionEvent(EVENT_CREATED);
    }

    /// <summary>
    /// Records that EF Core started closing a database connection.
    /// </summary>
    public void RecordConnectionClosing()
    {
        RecordConnectionEvent(EVENT_CLOSING);
    }

    /// <summary>
    /// Records that EF Core closed a database connection.
    /// </summary>
    public void RecordConnectionClosed()
    {
        RecordConnectionEvent(EVENT_CLOSED);
    }

    /// <summary>
    /// Records that EF Core failed while working with a database connection.
    /// </summary>
    public void RecordConnectionFailed()
    {
        RecordConnectionEvent(EVENT_FAILED);
    }

    /// <summary>
    /// Records that EF Core started opening a database connection.
    /// </summary>
    public void RecordConnectionOpening()
    {
        RecordConnectionEvent(EVENT_OPENING);
    }

    /// <summary>
    /// Records that EF Core opened a database connection.
    /// </summary>
    public void RecordConnectionOpened()
    {
        RecordConnectionEvent(EVENT_OPENED);
    }

    /// <summary>
    /// Records that EF Core started disposing a database connection.
    /// </summary>
    public void RecordConnectionDisposing()
    {
        Interlocked.Increment(ref _connectionDisposingCount);
        RecordConnectionEvent(EVENT_DISPOSING);
    }

    /// <summary>
    /// Records that EF Core disposed a database connection.
    /// </summary>
    public void RecordConnectionDisposed()
    {
        Interlocked.Increment(ref _connectionDisposedCount);
        RecordConnectionEvent(EVENT_DISPOSED);
    }

    private void RecordConnectionEvent(string eventName)
    {
        _connectionEvents.Add(1, new KeyValuePair<string, object?>(EVENT_TAG_NAME, eventName));
    }

    private Measurement<long>[] ObserveCurrentConnectionStates()
    {
        return
        [
            new(
                Volatile.Read(ref _connectionCreatingCount) - Volatile.Read(ref _connectionCreatedCount),
                new KeyValuePair<string, object?>(STATE_TAG_NAME, STATE_CREATING)),
            new(
                Volatile.Read(ref _connectionDisposingCount) - Volatile.Read(ref _connectionDisposedCount),
                new KeyValuePair<string, object?>(STATE_TAG_NAME, STATE_DISPOSING)),
            new(
                Volatile.Read(ref _connectionCreatedCount) - Volatile.Read(ref _connectionDisposedCount),
                new KeyValuePair<string, object?>(STATE_TAG_NAME, STATE_NOT_CLOSED))
        ];
    }
}
