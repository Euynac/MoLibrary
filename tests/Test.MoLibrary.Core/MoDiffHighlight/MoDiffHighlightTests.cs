using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.MoDiffHighlight;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Modules;
using NSubstitute;
using Xunit;

namespace Test.MoLibrary.Core.MoDiffHighlight;

public class MoDiffHighlightTests
{
    private readonly IMoDiffHighlight _diffHighlight;
    private readonly ILogger<DefaultDiffHighlight> _mockLogger;
    private readonly IOptions<ModuleDiffHighlightOption> _mockOptions;

    public MoDiffHighlightTests()
    {
        _mockLogger = Substitute.For<ILogger<DefaultDiffHighlight>>();
        _mockOptions = Substitute.For<IOptions<ModuleDiffHighlightOption>>();
        _mockOptions.Value.Returns(new ModuleDiffHighlightOption());
        
        _diffHighlight = new DefaultDiffHighlight(_mockLogger, _mockOptions);
    }

    [Fact]
    public void Highlight_IdenticalStrings_ShouldReturnNoChanges()
    {
        // Arrange
        var text = "Hello World";

        // Act
        var result = _diffHighlight.Highlight(text, text);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(0);
        result.Statistics.AddedLines.Should().Be(0);
        result.Statistics.DeletedLines.Should().Be(0);
        result.Statistics.ModifiedLines.Should().Be(0);
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Highlight_CompletelyDifferentStrings_ShouldDetectAllChanges()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "Goodbye Universe";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().BeGreaterThan(0);
        result.Lines.Should().HaveCount(2);
        
        result.Lines[0].Type.Should().Be(EDiffLineType.Deleted);
        result.Lines[0].OldContent.Should().Be("Hello World");
        
        result.Lines[1].Type.Should().Be(EDiffLineType.Added);
        result.Lines[1].NewContent.Should().Be("Goodbye Universe");
    }

    [Fact]
    public void Highlight_MultilineText_ShouldDetectLineChanges()
    {
        // Arrange
        var oldText = "Line 1\nLine 2\nLine 3";
        var newText = "Line 1\nModified Line 2\nLine 3";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(1);
        result.Statistics.ModifiedLines.Should().Be(1);
        
        var modifiedLines = result.Lines.Where(d => d.Type == EDiffLineType.Deleted || d.Type == EDiffLineType.Added).ToList();
        modifiedLines.Should().HaveCount(2);
        modifiedLines.Any(l => l.OldContent.Contains("Line 2") || l.NewContent.Contains("Line 2")).Should().BeTrue();
        modifiedLines.Any(l => l.OldContent.Contains("Modified Line 2") || l.NewContent.Contains("Modified Line 2")).Should().BeTrue();
    }

    [Fact]
    public void Highlight_AddedLines_ShouldDetectAdditions()
    {
        // Arrange
        var oldText = "Line 1\nLine 2";
        var newText = "Line 1\nLine 2\nLine 3";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(1);
        result.Statistics.AddedLines.Should().Be(1);
        
        result.Lines.Last().Type.Should().Be(EDiffLineType.Added);
        result.Lines.Last().NewContent.Should().Be("Line 3");
    }

    [Fact]
    public void Highlight_RemovedLines_ShouldDetectDeletions()
    {
        // Arrange
        var oldText = "Line 1\nLine 2\nLine 3";
        var newText = "Line 1\nLine 2";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(1);
        result.Statistics.DeletedLines.Should().Be(1);
        
        var deletedLine = result.Lines.First(d => d.Type == EDiffLineType.Deleted);
        deletedLine.OldContent.Should().Be("Line 3");
    }

    [Fact]
    public void Highlight_EmptyStrings_ShouldReturnNoChanges()
    {
        // Arrange
        var oldText = "";
        var newText = "";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(0);
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Highlight_EmptyToNonEmpty_ShouldDetectAllAdditions()
    {
        // Arrange
        var oldText = "";
        var newText = "New content";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(1);
        result.Statistics.AddedLines.Should().Be(1);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Type.Should().Be(EDiffLineType.Added);
    }

    [Fact]
    public void Highlight_NonEmptyToEmpty_ShouldDetectAllDeletions()
    {
        // Arrange
        var oldText = "Content to delete";
        var newText = "";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(1);
        result.Statistics.DeletedLines.Should().Be(1);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Type.Should().Be(EDiffLineType.Deleted);
    }

    [Fact]
    public async Task HighlightAsync_ShouldProducesSameResultAsSync()
    {
        // Arrange
        var oldText = "Line 1\nLine 2\nLine 3";
        var newText = "Line 1\nModified Line 2\nLine 3\nLine 4";

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

    [Fact]
    public void Highlight_WithOptions_ShouldRespectIgnoreWhitespace()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "Hello  World"; // Extra space
        var options = new DiffHighlightOptions
        {
            IgnoreWhitespace = true
        };

        // Act
        var result = _diffHighlight.Highlight(oldText, newText, options);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(0);
    }

    [Fact]
    public void Highlight_ProcessingTime_ShouldBeRecorded()
    {
        // Arrange
        var oldText = "Some content";
        var newText = "Some modified content";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingTimeMs.Should().BeGreaterThan(0);
    }
}