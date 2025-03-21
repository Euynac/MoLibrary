namespace MoLibrary.Locker.Providers.Dapr;

public class MoDistributedLockDaprOptions
{
    public string StoreName { get; set; } = default!;

    public string? Owner { get; set; }

    public TimeSpan DefaultExpirationTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
