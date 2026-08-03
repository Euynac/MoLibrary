using Microsoft.AspNetCore.Components;

namespace Test.Monica.Markdown.Support;

internal sealed class TestNavigationManager : NavigationManager
{
    private const string BASE_URI = "https://localhost/";

    internal TestNavigationManager(string initialRelativeUri = "/markdown-docs")
    {
        var initialUri = new Uri(new Uri(BASE_URI), initialRelativeUri).AbsoluteUri;
        Initialize(BASE_URI, initialUri);
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        SetLocation(uri);
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        SetLocation(uri);
    }

    private void SetLocation(string uri)
    {
        Uri = ToAbsoluteUri(uri).AbsoluteUri;
        NotifyLocationChanged(isInterceptedLink: false);
    }
}
