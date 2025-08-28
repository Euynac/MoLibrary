using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.MoDiffHighlight;
using MoLibrary.Core.Features.MoDiffHighlight.Algorithms;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Features.MoDiffHighlight.Renderers;
using MoLibrary.Core.Modules;
using NSubstitute;
using Xunit;

namespace Test.MoLibrary.Core.MoDiffHighlight;

/// <summary>
/// Integration tests for the complete DiffHighlight workflow
/// </summary>
public class DiffHighlightIntegrationTests
{
    private readonly IMoDiffHighlight _diffHighlight;

    public DiffHighlightIntegrationTests()
    {
        var mockLogger = Substitute.For<ILogger<DefaultDiffHighlight>>();
        var mockOptions = Substitute.For<IOptions<ModuleDiffHighlightOption>>();
        mockOptions.Value.Returns(new ModuleDiffHighlightOption());
        
        _diffHighlight = new DefaultDiffHighlight(mockLogger, mockOptions);
    }

    [Fact]
    public void CompleteWorkflow_SimpleTextDiff_ShouldProduceExpectedResults()
    {
        // Arrange
        var oldText = "Hello World\nThis is line 2\nThis is line 3";
        var newText = "Hello Universe\nThis is line 2\nThis is modified line 3";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(2);
        result.Statistics.ModifiedLines.Should().Be(2);
        result.Lines.Should().NotBeEmpty();
        result.HighlightedContent.Should().NotBeNullOrEmpty();
        result.ProcessingTimeMs.Should().BeGreaterThan(0);

        // Verify specific line changes
        var deletedLines = result.Lines.Where(d => d.Type == EDiffLineType.Deleted).ToList();
        var addedLines = result.Lines.Where(d => d.Type == EDiffLineType.Added).ToList();
        
        deletedLines.Should().Contain(d => d.OldContent.Contains("World"));
        deletedLines.Should().Contain(d => d.OldContent.Contains("line 3"));
        addedLines.Should().Contain(d => d.NewContent.Contains("Universe"));
        addedLines.Should().Contain(d => d.NewContent.Contains("modified"));
    }

    [Fact]
    public async Task CompleteAsyncWorkflow_ShouldMatchSyncResults()
    {
        // Arrange
        var oldText = "Original content\nWith multiple lines\nFor testing";
        var newText = "Modified content\nWith multiple lines\nFor comprehensive testing";

        // Act
        var syncResult = _diffHighlight.Highlight(oldText, newText);
        var asyncResult = await _diffHighlight.HighlightAsync(oldText, newText);

        // Assert
        asyncResult.Should().NotBeNull();
        asyncResult.Statistics.TotalChanges.Should().Be(syncResult.Statistics.TotalChanges);
        asyncResult.Statistics.AddedLines.Should().Be(syncResult.Statistics.AddedLines);
        asyncResult.Statistics.DeletedLines.Should().Be(syncResult.Statistics.DeletedLines);
        asyncResult.Statistics.ModifiedLines.Should().Be(syncResult.Statistics.ModifiedLines);
        asyncResult.Lines.Should().HaveCount(syncResult.Lines.Count);
    }

    [Theory]
    [InlineData(typeof(HtmlDiffRenderer))]
    [InlineData(typeof(MarkdownDiffRenderer))]
    [InlineData(typeof(PlainTextDiffRenderer))]
    public void CompleteWorkflow_WithDifferentRenderers_ShouldProduceValidOutput(Type rendererType)
    {
        // Arrange
        var renderer = (IDiffHighlightRenderer)Activator.CreateInstance(rendererType)!;
        _diffHighlight.SetRenderer(renderer);

        var oldText = "Original text\nLine 2\nLine 3";
        var newText = "Modified text\nLine 2 changed\nLine 3";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.HighlightedContent.Should().NotBeNullOrEmpty();
        result.Statistics.TotalChanges.Should().BeGreaterThan(0);

        // Verify renderer-specific output characteristics
        switch (rendererType.Name)
        {
            case nameof(HtmlDiffRenderer):
                result.HighlightedContent.Should().Contain("<!DOCTYPE html>");
                result.HighlightedContent.Should().Contain("diff-container");
                break;
            case nameof(MarkdownDiffRenderer):
                result.HighlightedContent.Should().Contain("# Text Diff");
                result.HighlightedContent.Should().Contain("```diff");
                break;
            case nameof(PlainTextDiffRenderer):
                result.HighlightedContent.Should().Contain("TEXT DIFF REPORT");
                result.HighlightedContent.Should().Contain("STATISTICS");
                break;
        }
    }

    [Fact]
    public void CompleteWorkflow_WithOptions_ShouldRespectSettings()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "HELLO WORLD"; // Case difference

        var optionsIgnoreCase = new DiffHighlightOptions { IgnoreCase = true };
        var optionsDefault = new DiffHighlightOptions { IgnoreCase = false };

        // Act
        var resultWithIgnoreCase = _diffHighlight.Highlight(oldText, newText, optionsIgnoreCase);
        var resultDefault = _diffHighlight.Highlight(oldText, newText, optionsDefault);

        // Assert
        resultWithIgnoreCase.Statistics.TotalChanges.Should().Be(0);
        resultDefault.Statistics.TotalChanges.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompleteWorkflow_RealWorldScenario_CodeDiff()
    {
        // Arrange
        var oldCode = @"public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
    
    public int Multiply(int a, int b)
    {
        return a * b;
    }
}";

        var newCode = @"public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
    
    public int Subtract(int a, int b)
    {
        return a - b;
    }
    
    public int Multiply(int a, int b)
    {
        return a * b;
    }
}";

        // Act
        var result = _diffHighlight.Highlight(oldCode, newCode);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().BeGreaterThan(0);
        result.Statistics.AddedLines.Should().BeGreaterThan(0);
        result.HighlightedContent.Should().Contain("Subtract");
        
        var addedLines = result.Lines.Where(d => d.Type == EDiffLineType.Added).ToList();
        addedLines.Should().Contain(d => d.NewContent.Contains("Subtract"));
    }

    [Fact]
    public void CompleteWorkflow_PerformanceTest_LargeFiles()
    {
        // Arrange
        var lines = Enumerable.Range(1, 1000).Select(i => $"This is line {i} with some content").ToArray();
        var oldText = string.Join("\n", lines);
        
        // Modify some lines in the middle
        lines[500] = "This is modified line 501 with different content";
        lines[501] = "This is another modified line 502 with different content";
        var newText = string.Join("\n", lines);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _diffHighlight.Highlight(oldText, newText);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(2);
        result.Statistics.ModifiedLines.Should().Be(2);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should complete within 10 seconds
        result.ProcessingTimeMs.Should().BeLessThan(10000);
    }

    [Fact]
    public void CompleteWorkflow_AlgorithmAccuracy_MyersDiff()
    {
        // Arrange - Test case specifically designed to validate Myers algorithm
        var algorithm = new MyersDiffAlgorithm();
        var oldLines = new[] { "A", "B", "C", "D", "E" };
        var newLines = new[] { "A", "1", "C", "3", "E" }; // Replace B,D with 1,3
        var options = new DiffHighlightOptions();

        // Act
        var diffs = algorithm.ComputeDiff(oldLines, newLines, options);

        // Assert
        diffs.Should().NotBeNull();
        
        // Should detect deletions and additions
        var deletions = diffs.Where(d => d.Type == EDiffLineType.Deleted).ToList();
        var additions = diffs.Where(d => d.Type == EDiffLineType.Added).ToList();
        
        deletions.Should().NotBeEmpty();
        additions.Should().NotBeEmpty();
        deletions.Should().Contain(d => d.OldContent == "B");
        deletions.Should().Contain(d => d.OldContent == "D");
        additions.Should().Contain(d => d.NewContent == "1");
        additions.Should().Contain(d => d.NewContent == "3");
    }

    [Fact]
    public void CompleteWorkflow_EndToEnd_AllComponents()
    {
        // This test verifies the complete integration of all components
        
        // Arrange
        var oldText = "Line 1\nLine 2 original\nLine 3\nLine 4";
        var newText = "Line 1\nLine 2 modified\nNew Line\nLine 3\nLine 4";

        // Act - Test with different renderers
        var htmlResult = _diffHighlight.Highlight(oldText, newText);
        
        _diffHighlight.SetRenderer(new MarkdownDiffRenderer());
        var markdownResult = _diffHighlight.Highlight(oldText, newText);
        
        _diffHighlight.SetRenderer(new PlainTextDiffRenderer());
        var plainTextResult = _diffHighlight.Highlight(oldText, newText);

        // Assert - All should have same statistics but different formatting
        var results = new[] { htmlResult, markdownResult, plainTextResult };
        
        foreach (var result in results)
        {
            result.Should().NotBeNull();
            result.Statistics.TotalChanges.Should().Be(2); // One modified, one added
            result.Statistics.ModifiedLines.Should().Be(1);
            result.Statistics.AddedLines.Should().Be(1);
            result.Lines.Should().HaveCount(7); // Context + changes
            result.HighlightedContent.Should().NotBeNullOrEmpty();
        }

        // Verify unique formatting
        htmlResult.HighlightedContent.Should().Contain("<div");
        markdownResult.HighlightedContent.Should().Contain("```diff");
        plainTextResult.HighlightedContent.Should().StartWith("TEXT DIFF REPORT");
    }
}