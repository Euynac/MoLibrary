namespace Monica.StateStore.UI.Models;

[Flags]
public enum EStateStoreBrowserFeatures
{
    None = 0,
    ExactLookup = 1 << 0,
    PatternSearch = 1 << 1,
    ValuePreview = 1 << 2,
    Create = 1 << 3,
    Update = 1 << 4,
    Delete = 1 << 5,
    BulkDelete = 1 << 6,
    TimeToLive = 1 << 7
}

public enum EStateStoreKeySearchMode
{
    Auto,
    ExactKey,
    PatternScan
}

public enum EStateStoreKeyBrowseOutcome
{
    Success,
    Missing
}

public sealed record StateStoreKeyBrowseRequest
{
    public required string Query { get; init; }

    public int Limit { get; init; } = 100;

    public EStateStoreKeySearchMode SearchMode { get; init; } = EStateStoreKeySearchMode.Auto;
}

public sealed record StateStoreKeyBrowseResult
{
    public string Query { get; init; } = string.Empty;

    public IReadOnlyList<StateStoreKeyInfo> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public bool HasMore { get; init; }

    public EStateStoreKeySearchMode AppliedMode { get; init; }

    public EStateStoreKeyBrowseOutcome Outcome { get; init; } = EStateStoreKeyBrowseOutcome.Success;

    public bool IsMissing => Outcome == EStateStoreKeyBrowseOutcome.Missing;
}
