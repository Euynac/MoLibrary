using Monica.StateStore.Providers;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Services.Browser;

public interface IStateStoreBrowserApi
{
    bool CanHandle(EStateStoreProviderType providerType, IMoStateStore provider);

    EStateStoreBrowserFeatures GetFeatures(IMoStateStore provider);

    EStateStoreKeySearchMode GetDefaultSearchMode(IMoStateStore provider);

    Task<StateStoreKeyBrowseResult> BrowseAsync(
        IMoStateStore provider,
        StateStoreKeyBrowseRequest request,
        CancellationToken cancellationToken = default);

    Task<StateStoreKeyInfo> LoadKeyAsync(
        IMoStateStore provider,
        string key,
        CancellationToken cancellationToken = default);
}
