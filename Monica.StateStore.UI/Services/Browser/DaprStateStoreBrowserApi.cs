using Monica.StateStore.StateStore.Abstractions;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Services.Browser;

public sealed class DaprStateStoreBrowserApi : StateStoreBrowserApiBase
{
    public override EStateStoreProviderType ProviderType => EStateStoreProviderType.Dapr;

    public override EStateStoreBrowserFeatures GetFeatures(IStateStore provider)
    {
        return base.GetFeatures(provider) |
               EStateStoreBrowserFeatures.BulkDelete;
    }
}
