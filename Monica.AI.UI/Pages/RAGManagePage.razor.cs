using Microsoft.AspNetCore.Components;
using Monica.AI.UI.UIRAG.State;

namespace Monica.AI.UI.Pages;

public partial class RAGManagePage : IDisposable
{
    public const string PAGE_URL = "/ai/rag/manage";

    [Inject]
    public required RAGManagePageState PageState { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        PageState.StateChanged += OnStateChanged;
        await PageState.InitializeAsync();
    }

    private void OnStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        PageState.StateChanged -= OnStateChanged;
        PageState.Dispose();
    }
}
