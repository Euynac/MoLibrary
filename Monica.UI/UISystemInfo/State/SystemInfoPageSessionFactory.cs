using Monica.UI.UISystemInfo.Facades;

namespace Monica.UI.UISystemInfo.State;

/// <summary>
/// Creates explicitly page-owned System Info sessions without extending their disposable lifetime through dependency injection.
/// </summary>
public sealed class SystemInfoPageSessionFactory
{
    private readonly SystemInfoFacadeCalls _calls;

    /// <summary>Creates a factory backed by the host-local System Info facade.</summary>
    /// <param name="facade">The UI boundary that captures snapshots and requests a host restart.</param>
    public SystemInfoPageSessionFactory(SystemInfoFacade facade)
        : this(new SystemInfoFacadeCalls(facade.GetSnapshot, facade.RequestSelfRestart))
    {
    }

    internal SystemInfoPageSessionFactory(SystemInfoFacadeCalls calls)
    {
        _calls = calls;
    }

    /// <summary>Creates a fresh session owned by one rendered page instance.</summary>
    public SystemInfoPageSession Create() => new(_calls);
}
