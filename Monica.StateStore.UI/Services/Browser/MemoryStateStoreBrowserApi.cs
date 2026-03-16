using Monica.StateStore.Providers;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Services.Browser;

public sealed class MemoryStateStoreBrowserApi : StateStoreBrowserApiBase
{
    public override EStateStoreProviderType ProviderType => EStateStoreProviderType.Memory;

    public override EStateStoreBrowserFeatures GetFeatures(IMoStateStore provider)
    {
        return base.GetFeatures(provider) |
               EStateStoreBrowserFeatures.PatternSearch |
               EStateStoreBrowserFeatures.BulkDelete;
    }

    protected override async Task<StateStoreKeyBrowseResult> BrowsePatternAsync(
        IMoStateStore provider,
        string pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        var keys = await provider.ScanKeysAsync(pattern, cancellationToken);

        return new StateStoreKeyBrowseResult
        {
            Items = keys.Take(limit).Select(CreateKeyPlaceholder).ToList(),
            TotalCount = keys.Count,
            HasMore = keys.Count > limit,
            AppliedMode = EStateStoreKeySearchMode.PatternScan
        };
    }
}
