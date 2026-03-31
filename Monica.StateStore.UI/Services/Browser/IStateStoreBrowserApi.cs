using Monica.StateStore.StateStore.Abstractions;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Services.Browser;

public interface IStateStoreBrowserApi
{
    bool CanHandle(EStateStoreProviderType providerType, IStateStore provider);

    EStateStoreBrowserFeatures GetFeatures(IStateStore provider);

    EStateStoreKeySearchMode GetDefaultSearchMode(IStateStore provider);

    Task<StateStoreKeyBrowseResult> BrowseAsync(
        IStateStore provider,
        StateStoreKeyBrowseRequest request,
        CancellationToken cancellationToken = default);

    Task<StateStoreKeyInfo> LoadKeyAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken = default);
}
