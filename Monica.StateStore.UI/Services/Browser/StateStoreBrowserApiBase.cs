using System.Text.Json;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Models;
using Monica.StateStore.UI.Models;

namespace Monica.StateStore.UI.Services.Browser;

public abstract class StateStoreBrowserApiBase : IStateStoreBrowserApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public abstract EStateStoreProviderType ProviderType { get; }

    public virtual bool CanHandle(EStateStoreProviderType providerType, IStateStore provider)
    {
        return providerType == ProviderType;
    }

    public virtual EStateStoreBrowserFeatures GetFeatures(IStateStore provider)
    {
        var features = EStateStoreBrowserFeatures.ExactLookup |
                       EStateStoreBrowserFeatures.ValuePreview |
                       EStateStoreBrowserFeatures.Create |
                       EStateStoreBrowserFeatures.Update |
                       EStateStoreBrowserFeatures.Delete;

        if (provider is IStateStoreKeyTtlReader)
        {
            features |= EStateStoreBrowserFeatures.TimeToLive;
        }

        return features;
    }

    public virtual EStateStoreKeySearchMode GetDefaultSearchMode(IStateStore provider)
    {
        return GetFeatures(provider).HasFlag(EStateStoreBrowserFeatures.PatternSearch)
            ? EStateStoreKeySearchMode.PatternScan
            : EStateStoreKeySearchMode.ExactKey;
    }

    public async Task<StateStoreKeyBrowseResult> BrowseAsync(
        IStateStore provider,
        StateStoreKeyBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var query = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("Key query cannot be empty.");
        }

        var appliedMode = ResolveSearchMode(provider, request);

        var result = appliedMode switch
        {
            EStateStoreKeySearchMode.ExactKey => await BrowseExactKeyAsync(provider, query, cancellationToken),
            EStateStoreKeySearchMode.PatternScan => await BrowsePatternAsync(provider, query, request.Limit, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported search mode: {appliedMode}.")
        };

        return result with
        {
            Query = query
        };
    }

    public virtual async Task<StateStoreKeyInfo> LoadKeyAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!await provider.ExistAsync(key, cancellationToken))
        {
            throw new KeyNotFoundException($"未找到 Key: {key}");
        }

        var rawValueTask = LoadRawValueAsync(provider, key, cancellationToken);
        var etagTask = LoadETagAsync(provider, key, cancellationToken);
        var ttlTask = LoadTtlSnapshotAsync(provider, key, cancellationToken);

        await Task.WhenAll(rawValueTask, etagTask, ttlTask);
        var ttlSnapshot = ttlTask.Result;

        return new StateStoreKeyInfo
        {
            Key = key,
            RawValue = rawValueTask.Result,
            IsValueLoaded = true,
            ETag = etagTask.Result,
            TTL = ttlSnapshot.Remaining,
            TTLStatus = ttlSnapshot.Status
        };
    }

    protected virtual EStateStoreKeySearchMode ResolveSearchMode(
        IStateStore provider,
        StateStoreKeyBrowseRequest request)
    {
        if (request.SearchMode != EStateStoreKeySearchMode.Auto)
        {
            return request.SearchMode;
        }

        return GetFeatures(provider).HasFlag(EStateStoreBrowserFeatures.PatternSearch)
            ? EStateStoreKeySearchMode.PatternScan
            : EStateStoreKeySearchMode.ExactKey;
    }

    protected virtual async Task<StateStoreKeyBrowseResult> BrowseExactKeyAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken)
    {
        if (!await provider.ExistAsync(key, cancellationToken))
        {
            return new StateStoreKeyBrowseResult
            {
                AppliedMode = EStateStoreKeySearchMode.ExactKey,
                Outcome = EStateStoreKeyBrowseOutcome.Missing
            };
        }

        return new StateStoreKeyBrowseResult
        {
            Items =
            [
                await LoadKeyAsync(provider, key, cancellationToken)
            ],
            TotalCount = 1,
            AppliedMode = EStateStoreKeySearchMode.ExactKey
        };
    }

    protected virtual Task<StateStoreKeyBrowseResult> BrowsePatternAsync(
        IStateStore provider,
        string pattern,
        int limit,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException($"{ProviderType} provider does not support pattern-based key browsing.");
    }

    protected static StateStoreKeyInfo CreateKeyPlaceholder(string key)
    {
        return new StateStoreKeyInfo
        {
            Key = key
        };
    }

    protected virtual async Task<string?> LoadRawValueAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken)
    {
        if (provider is IDistributedStateStore distributedProvider)
        {
            return await distributedProvider.GetRawStateAsync(key, cancellationToken);
        }

        var value = await provider.GetStateAsync<object>(key, cancellationToken);
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    protected virtual async Task<string?> LoadETagAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken)
    {
        if (provider is IDistributedStateStore)
        {
            var (_, distributedEtag) = await provider.GetStateAndETagAsync<JsonElement?>(key, cancellationToken);
            return string.IsNullOrWhiteSpace(distributedEtag) ? null : distributedEtag;
        }

        var (_, etag) = await provider.GetStateAndETagAsync<object>(key, cancellationToken);
        return string.IsNullOrWhiteSpace(etag) ? null : etag;
    }

    protected virtual Task<StateStoreKeyTtlSnapshot> LoadTtlSnapshotAsync(
        IStateStore provider,
        string key,
        CancellationToken cancellationToken)
    {
        return provider is IStateStoreKeyTtlReader ttlReader
            ? ttlReader.GetKeyTtlAsync(key, cancellationToken)
            : Task.FromResult(StateStoreKeyTtlSnapshot.Unsupported);
    }
}
