namespace Monica.StateStore.StateStore.Models;

public enum EStateStoreKeyTtlStatus
{
    Unknown = 0,
    Unsupported = 1,
    Permanent = 2,
    Expiring = 3
}

public sealed record StateStoreKeyTtlSnapshot
{
    public static StateStoreKeyTtlSnapshot Unknown { get; } = new()
    {
        Status = EStateStoreKeyTtlStatus.Unknown
    };

    public static StateStoreKeyTtlSnapshot Unsupported { get; } = new()
    {
        Status = EStateStoreKeyTtlStatus.Unsupported
    };

    public static StateStoreKeyTtlSnapshot Permanent { get; } = new()
    {
        Status = EStateStoreKeyTtlStatus.Permanent
    };

    public EStateStoreKeyTtlStatus Status { get; init; } = EStateStoreKeyTtlStatus.Unknown;

    public TimeSpan? Remaining { get; init; }

    public static StateStoreKeyTtlSnapshot FromRemaining(TimeSpan remaining)
    {
        return new StateStoreKeyTtlSnapshot
        {
            Status = EStateStoreKeyTtlStatus.Expiring,
            Remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining
        };
    }
}

public interface IStateStoreKeyTtlReader
{
    Task<StateStoreKeyTtlSnapshot> GetKeyTtlAsync(string key, CancellationToken cancellationToken = default);
}
