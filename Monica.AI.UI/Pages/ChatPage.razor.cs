using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.AI.UI.Localization;
using Monica.AI.UI.UIChat.State;

namespace Monica.AI.UI.Pages;

public partial class ChatPage : IDisposable
{
    public const string PAGE_URL = "/ai-chat";

    [Inject]
    public required ChatPageState PageState { get; set; }

    [Inject]
    public required IStringLocalizer<AIResource> L { get; set; }

    private bool _sessionDrawerOpen = true;

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

    private void ToggleSessionDrawer()
    {
        _sessionDrawerOpen = !_sessionDrawerOpen;
    }

    public void Dispose()
    {
        PageState.StateChanged -= OnStateChanged;
        PageState.Dispose();
    }
}
