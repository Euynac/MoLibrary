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

public class EdgeCasesAndErrorHandlingTests
{
    private readonly IMoDiffHighlight _diffHighlight;
    private readonly MyersDiffAlgorithm _algorithm;

    public EdgeCasesAndErrorHandlingTests()
    {
        var mockLogger = Substitute.For<ILogger<DefaultDiffHighlight>>();
        var mockOptions = Substitute.For<IOptions<ModuleDiffHighlightOption>>();
        mockOptions.Value.Returns(new ModuleDiffHighlightOption());
        
        _diffHighlight = new DefaultDiffHighlight(mockLogger, mockOptions);
        _algorithm = new MyersDiffAlgorithm();
    }

    [Fact]
    public void Highlight_WithNullInput_ShouldHandleGracefully()
    {
        // The implementation should handle null values by treating them as empty strings
        // Act
        var result1 = _diffHighlight.Highlight(null!, "new text");
        var result2 = _diffHighlight.Highlight("old text", null!);

        // Assert - should not throw, should handle gracefully
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public void Highlight_WithEmptyStrings_ShouldReturnNoChanges()
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
    public void Highlight_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var oldText = "Hello 世界! 🌍🚀";
        var newText = "Hi 世界! 🌎🚀";

        // Act
        var result = _diffHighlight.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().BeGreaterThan(0);
        result.Lines.Should().NotBeEmpty();
    }

    [Fact]
    public void Renderers_RenderDiff_WithValidInput_ShouldProduceOutput()
    {
        // Arrange
        var renderers = new IDiffHighlightRenderer[]
        {
            new HtmlDiffRenderer(),
            new MarkdownDiffRenderer(),
            new PlainTextDiffRenderer()
        };

        var result = new DiffHighlightResult
        {
            Lines = new List<DiffLine>
            {
                new() { Type = EDiffLineType.Added, NewContent = "Test line", NewLineNumber = 1 }
            },
            Statistics = new DiffStatistics { TotalChanges = 1, AddedLines = 1 }
        };

        // Act & Assert
        foreach (var renderer in renderers)
        {
            var output = renderer.Render(result.Lines, new DiffHighlightStyle());
            output.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Algorithm_ComputeDiff_WithValidInput_ShouldWork()
    {
        // Arrange
        var oldLines = new[] { "Hello" };
        var newLines = new[] { "Hi" };
        var options = new DiffHighlightOptions();

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, options);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // 1 deletion + 1 addition
    }

    [Fact]
    public void Highlight_WithOptions_ShouldRespectSettings()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "HELLO WORLD"; // Case difference
        var options = new DiffHighlightOptions { IgnoreCase = true };

        // Act
        var result = _diffHighlight.Highlight(oldText, newText, options);

        // Assert
        result.Should().NotBeNull();
        result.Statistics.TotalChanges.Should().Be(0); // Should ignore case differences
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