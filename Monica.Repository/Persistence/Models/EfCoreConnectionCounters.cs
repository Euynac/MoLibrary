namespace Monica.Repository.Persistence.Models;

/// <summary>
/// Aggregates process-wide EF Core connection lifecycle counters.
/// </summary>
public class EfCoreConnectionCounters
{
    public int CurrentCreatingCount => ConnectionCreatingCount - ConnectionCreatedCount;
    public int CurrentDisposingCount => ConnectionDisposingCount - ConnectionDisposedCount;
    public int CurrentNotClosedCount => ConnectionCreatedCount - ConnectionDisposedCount;
    public int TotalCreatingCount => ConnectionCreatingCount;
    public int TotalCreatedCount => ConnectionCreatedCount;
    public int TotalClosingCount => ConnectionClosingCount;
    public int TotalClosedCount => ConnectionClosedCount;
    public int TotalFailedCount => ConnectionFailedCount;
    public int TotalOpeningCount => ConnectionOpeningCount;
    public int TotalOpenedCount => ConnectionOpenedCount;
    public int TotalDisposingCount => ConnectionDisposingCount;
    public int TotalDisposedCount => ConnectionDisposedCount;

    public static int ConnectionCreatingCount;
    public static int ConnectionCreatedCount;
    public static int ConnectionClosingCount;
    public static int ConnectionClosedCount;
    public static int ConnectionFailedCount;
    public static int ConnectionOpeningCount;
    public static int ConnectionOpenedCount;
    public static int ConnectionDisposingCount;
    public static int ConnectionDisposedCount;
}
