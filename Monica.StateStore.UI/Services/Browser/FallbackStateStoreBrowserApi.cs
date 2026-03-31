using Monica.StateStore.Abstractions;

namespace Monica.StateStore.UI.Services.Browser;

public sealed class FallbackStateStoreBrowserApi : StateStoreBrowserApiBase
{
    public override EStateStoreProviderType ProviderType => EStateStoreProviderType.Unknown;

    public override bool CanHandle(EStateStoreProviderType providerType, IStateStore provider)
    {
        return true;
    }
}
