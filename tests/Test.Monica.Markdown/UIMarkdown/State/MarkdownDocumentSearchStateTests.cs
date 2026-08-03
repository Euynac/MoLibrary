using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Facades;
using Monica.Markdown.Models;
using Monica.Markdown.UIMarkdown.State;
using Monica.Modules;
using NSubstitute;

namespace Test.Monica.Markdown.UIMarkdown.State;

public sealed class MarkdownDocumentSearchStateTests
{
    [Fact]
    public async Task SearchAsync_WhenDisposedDuringProviderCall_ShouldIgnoreLateResultAndRemainIdempotent()
    {
        var searchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var searchCompletion = new TaskCompletionSource<IReadOnlyList<MarkdownDocumentSearchResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var searcher = Substitute.For<IMarkdownDocumentSearcher>();
        searcher.SearchAsync(
                Arg.Any<MarkdownDocumentSearchRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                searchStarted.TrySetResult();
                return searchCompletion.Task;
            });
        var state = new MarkdownDocumentSearchState(
            new MarkdownFacade(
                Substitute.For<IMarkdownDocumentCatalog>(),
                searcher,
                NullLogger<MarkdownFacade>.Instance),
            Options.Create(new ModuleMarkdownOption()),
            Options.Create(new ModuleMarkdownUIOption()),
            currentGroup: null,
            currentCulture: null);

        var search = state.OnQueryDebouncedAsync("lifetime");
        await searchStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        state.IsSearching.Should().BeTrue();

        var firstDisposal = state.DisposeAsync().AsTask();
        var secondDisposal = state.DisposeAsync().AsTask();
        firstDisposal.IsCompleted.Should().BeFalse();
        secondDisposal.IsCompleted.Should().BeFalse();
        state.IsSearching.Should().BeFalse();
        searchCompletion.TrySetResult([CreateResult()]);

        await Task.WhenAll(search, firstDisposal, secondDisposal);

        state.Query.Should().Be("lifetime");
        state.Results.Should().BeEmpty();
        state.IsSearching.Should().BeFalse();

        state.OnQueryChanged("late query");
        await state.OnIncludeAllKnowledgeBasesChangedAsync(true);

        state.Query.Should().Be("lifetime");
        state.IncludeAllKnowledgeBases.Should().BeFalse();
        state.Results.Should().BeEmpty();
    }

    private static MarkdownDocumentSearchResult CreateResult()
    {
        return new MarkdownDocumentSearchResult(
            "docs",
            "Documentation",
            "guide.md",
            "Guide",
            null,
            "Lifecycle",
            "lifetime result",
            [],
            "lifecycle",
            new MarkdownSearchLocator(
                "lifecycle",
                "lifetime",
                string.Empty,
                string.Empty,
                HeadingLevel: 2),
            Score: 1);
    }
}
