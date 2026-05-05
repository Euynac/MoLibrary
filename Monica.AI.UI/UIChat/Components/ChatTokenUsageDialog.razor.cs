using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.AI.Models;
using Monica.AI.UI.Localization;
using Monica.AI.UI.UIChat.Support;
using MudBlazor;

namespace Monica.AI.UI.UIChat.Components;

public partial class ChatTokenUsageDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IStringLocalizer<AIResource> L { get; set; } = null!;

    [Parameter]
    public string? ModelName { get; set; }

    [Parameter]
    public int ContextWindow { get; set; } = ChatProviderResolver.DEFAULT_CONTEXT_WINDOW;

    [Parameter]
    public IReadOnlyList<AIChatRequestUsage> RequestUsages { get; set; } = [];

    private TokenUsage? LatestUsage => RequestUsages.LastOrDefault()?.Usage;

    private TokenUsage? AggregateUsage => RequestUsages.Count > 0
        ? TokenUsage.Sum(RequestUsages.Select(request => request.Usage))
        : null;

    private int LatestInputTokens => LatestUsage?.InputTokens ?? 0;

    private int RemainingTokens => Math.Max(ContextWindow - LatestInputTokens, 0);

    private double RemainingRatio => ContextWindow > 0
        ? Math.Clamp((double)RemainingTokens / ContextWindow, 0, 1)
        : 1;

    private double RemainingPercent => RemainingRatio * 100;

    private Color RemainingColor => RemainingRatio switch
    {
        < 0.1 => Color.Error,
        < 0.25 => Color.Warning,
        _ => Color.Primary
    };

    private string RequestColumnLabel => L["Chat:Usage:Column:Request"];

    private string InputColumnLabel => L["Chat:Usage:Column:Input"];

    private string OutputColumnLabel => L["Chat:Usage:Column:Output"];

    private string CachedColumnLabel => L["Chat:Usage:Column:Cached"];

    private string CacheHitColumnLabel => L["Chat:Usage:Column:CacheHit"];

    private string TotalColumnLabel => L["Chat:Usage:Column:Total"];

    private void Close()
    {
        MudDialog.Close();
    }

    private static string FormatNumber(int value)
    {
        return value.ToString("N0");
    }

    private string FormatTokenCount(int tokens)
    {
        return tokens >= 1_000_000
            ? $"{tokens / 1_000_000d:0.#}M"
            : $"{tokens / 1_000d:0.#}K";
    }

    private static string FormatPercent(double ratio)
    {
        return $"{ratio:P0}";
    }

    private static string TrimIdentifier(string value)
    {
        return value.Length <= 18 ? value : $"{value[..8]}...{value[^6..]}";
    }
}
