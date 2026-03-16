using Monica.StateStore.Providers;

namespace Monica.StateStore.UI.Services.Browser;

public sealed class FallbackStateStoreBrowserApi : StateStoreBrowserApiBase
{
    public override EStateStoreProviderType ProviderType => EStateStoreProviderType.Unknown;

    public override bool CanHandle(EStateStoreProviderType providerType, IMoStateStore provider)
    {
        return true;
    }
}
