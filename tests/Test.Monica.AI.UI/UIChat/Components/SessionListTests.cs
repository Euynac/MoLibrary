using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.Localization;
using Monica.AI.UI.UIChat.Components;
using Monica.UnitTests.Localization;
using MudBlazor.Services;

namespace Test.Monica.AI.UI.UIChat.Components;

public sealed class SessionListTests : BunitContext
{
    public SessionListTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IStringLocalizer<AIResource>, EchoStringLocalizer<AIResource>>();
        _ = Render<MudBlazor.MudPopoverProvider>();
    }

    [Fact]
    public void Render_WhenHistoryIsLoading_ShouldExposeBusyStatusWithoutEmptyState()
    {
        var component = Render<SessionList>(parameters => parameters
            .Add(item => item.IsLoading, true));

        component.Find(".session-list-content").GetAttribute("aria-busy").Should().Be("true");
        component.Find("[role='status']").TextContent.Should().Contain("Chat:History:Loading");
        component.Markup.Should().NotContain("Session:NoConversations");
    }

    [Fact]
    public void Render_WhenSessionExists_ShouldExposeNamedDeleteAndClearActions()
    {
        var summary = new ChatSessionSummary
        {
            SessionId = "session-1",
            Title = "Conversation",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Settings = new ChatSessionSettings("provider", "model", null, false)
        };

        var component = Render<SessionList>(parameters => parameters
            .Add(item => item.Sessions, [summary])
            .Add(item => item.CurrentSessionId, summary.SessionId));

        component.Find("button[aria-label='Session:Delete']").Should().NotBeNull();
        component.Find("button[aria-label='Chat:History:Clear:Action']").Should().NotBeNull();
    }
}
