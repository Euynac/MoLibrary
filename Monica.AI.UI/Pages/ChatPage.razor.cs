using Microsoft.AspNetCore.Components;
using Monica.AI.UI.UIChat.State;

namespace Monica.AI.UI.Pages;

public partial class ChatPage : IDisposable
{
    public const string PAGE_URL = "/ai-chat";

    [Inject]
    public required ChatPageState PageState { get; set; }

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
