using Microsoft.AspNetCore.Components;
using Monica.AI.UI.UIKnowledgeBase.State;

namespace Monica.AI.UI.Pages;

public partial class KnowledgeBaseManagePage
{
    public const string PAGE_URL = "/ai/knowledge-bases";

    [Inject]
    private KnowledgeBaseManagePageState PageState { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        PageState.StateChanged += HandleStateChanged;
        await PageState.InitializeAsync();
    }

    private void HandleStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        PageState.StateChanged -= HandleStateChanged;
    }
}
