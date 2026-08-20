using AwesomeAssertions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Localization.Models;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Facades;
using Monica.Markdown.Localization;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.State;
using Monica.Testing.Localization;
using Monica.Tool.Algorithms.Trees;
using MudBlazor;
using NSubstitute;
using Test.Monica.Markdown.Support;

namespace Test.Monica.Markdown.UIMarkdown.State;

public sealed class MarkdownViewerPageStateTests
{
    private const string MARKDOWN_LAYOUT_MODULE_PATH =
        "./_content/Monica.Markdown/js/markdown-layout.js";

    [Fact]
    public async Task InitializeAsync_WhenDisposedDuringGroupLoad_ShouldIgnoreLateResultAndRender()
    {
        var groupsStarted = NewSignal();
        var groupsCompletion = new TaskCompletionSource<List<MarkdownDocumentGroup>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Substitute.For<IMarkdownDocumentCatalog>();
        catalog.GetAllDocumentGroupsAsync().Returns(_ =>
        {
            groupsStarted.TrySetResult();
            return groupsCompletion.Task;
        });
        var fixture = CreateFixture(catalog);

        var initialization = fixture.State.InitializeAsync();
        await groupsStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        var renderCountAtDisposal = fixture.RenderCount;

        var firstDisposal = fixture.State.DisposeAsync().AsTask();
        var secondDisposal = fixture.State.DisposeAsync().AsTask();
        firstDisposal.IsCompleted.Should().BeFalse();
        secondDisposal.IsCompleted.Should().BeFalse();
        groupsCompletion.TrySetResult([CreateGroup()]);

        await Task.WhenAll(initialization, firstDisposal, secondDisposal);

        fixture.State.Groups.Should().BeNull();
        fixture.State.IsLoading.Should().BeTrue();
        fixture.RenderCount.Should().Be(renderCountAtDisposal);
        fixture.Runtime.InvocationCount.Should().Be(0);

        fixture.State.ToggleSidebar();
        await fixture.State.OnHashChangedAsync("late-anchor");
        await fixture.State.InitializeAsync();

        fixture.State.ShowSidebar.Should().BeFalse();
        fixture.State.CurrentAnchorId.Should().BeNull();
        fixture.RenderCount.Should().Be(renderCountAtDisposal);
    }

    [Fact]
    public async Task LocationLoad_WhenDisposedDuringContentRead_ShouldNotPublishLateContentOrRender()
    {
        var document = CreateDocument();
        var group = CreateGroup(document);
        var contentStarted = NewSignal();
        var contentCompletion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Substitute.For<IMarkdownDocumentCatalog>();
        catalog.GetAllDocumentGroupsAsync().Returns(Task.FromResult(new List<MarkdownDocumentGroup> { group }));
        catalog.GetDocumentTreeAsync(group.Key, Arg.Any<string?>()).Returns(Task.FromResult(group.RootNode));
        catalog.GetDocumentContentAsync(document).Returns(_ =>
        {
            contentStarted.TrySetResult();
            return contentCompletion.Task;
        });
        var fixture = CreateFixture(
            catalog,
            "/markdown-docs?group=docs&document=guide.md");

        await fixture.State.InitializeAsync();
        await contentStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        fixture.State.SelectedDocument.Should().BeSameAs(document);
        fixture.State.IsContentLoading.Should().BeTrue();
        var renderCountAtDisposal = fixture.RenderCount;

        var disposal = fixture.State.DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse();
        contentCompletion.TrySetResult("late content");
        await disposal;

        fixture.State.DocumentContent.Should().BeNull();
        fixture.State.IsContentLoading.Should().BeTrue();
        fixture.State.SelectedDocument.Should().BeSameAs(document);
        fixture.RenderCount.Should().Be(renderCountAtDisposal);

        fixture.Navigation.NavigateTo(
            "/markdown-docs?group=docs&document=another.md");
        await Task.Yield();

        _ = catalog.Received(1).GetDocumentTreeAsync(group.Key, Arg.Any<string?>());
        _ = catalog.Received(1).GetDocumentContentAsync(document);
        fixture.RenderCount.Should().Be(renderCountAtDisposal);
    }

    private static ViewerStateFixture CreateFixture(
        IMarkdownDocumentCatalog catalog,
        string initialRelativeUri = "/markdown-docs")
    {
        var eventLog = new JsInteropEventLog();
        var import = new ControlledJsOperation(eventLog, "runtime.import");
        var runtime = new ControlledJsRuntime(
            eventLog,
            import,
            MARKDOWN_LAYOUT_MODULE_PATH);
        var navigation = new TestNavigationManager(initialRelativeUri);
        var facade = new MarkdownFacade(
            catalog,
            Substitute.For<IMarkdownDocumentSearcher>(),
            NullLogger<MarkdownFacade>.Instance);
        var fixture = new ViewerStateFixture(runtime, navigation);
        fixture.State = new MarkdownViewerPageState(
            facade,
            Substitute.For<IDialogService>(),
            navigation,
            Substitute.For<ISnackbar>(),
            runtime,
            new EchoStringLocalizer<MarkdownResource>(),
            LocalizationProfile.Create(
                "zh-CN",
                ["zh-CN", "en-US"],
                new Dictionary<string, string>
                {
                    ["zh-CN"] = "简体中文",
                    ["en-US"] = "English"
                },
                ".AspNetCore.Culture"));
        fixture.State.Attach(fixture.RequestRenderAsync);
        return fixture;
    }

    private static MarkdownDocumentGroup CreateGroup(MarkdownDocument? document = null)
    {
        var root = new TreeNode<MarkdownDocumentNodeData>(
            new MarkdownDocumentNodeData(string.Empty, IsDocument: false));

        return new MarkdownDocumentGroup
        {
            Key = "docs",
            Title = "Documentation",
            BasePath = "/docs",
            IsValid = true,
            DocumentCount = document is null ? 0 : 1,
            RootNode = root,
            Documents = document is null ? [] : [document]
        };
    }

    private static MarkdownDocument CreateDocument()
    {
        return new MarkdownDocument
        {
            GroupKey = "docs",
            Title = "Guide",
            NavigationTitle = "Guide",
            FilePath = "/docs/guide.md",
            RelativePath = "guide.md",
            NavigationRelativePath = "guide.md",
            Level = 0,
            FileSize = 42,
            LastModifiedUtc = DateTime.UnixEpoch
        };
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ViewerStateFixture(
        ControlledJsRuntime runtime,
        TestNavigationManager navigation)
    {
        private int _renderCount;

        internal MarkdownViewerPageState State { get; set; } = null!;

        internal ControlledJsRuntime Runtime { get; } = runtime;

        internal TestNavigationManager Navigation { get; } = navigation;

        internal int RenderCount => Volatile.Read(ref _renderCount);

        internal Task RequestRenderAsync()
        {
            Interlocked.Increment(ref _renderCount);
            return Task.CompletedTask;
        }
    }
}
