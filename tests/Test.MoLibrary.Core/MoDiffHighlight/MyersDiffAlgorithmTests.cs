using FluentAssertions;
using MoLibrary.Core.Features.MoDiffHighlight.Algorithms;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using Xunit;

namespace Test.MoLibrary.Core.MoDiffHighlight;

public class MyersDiffAlgorithmTests
{
    private readonly MyersDiffAlgorithm _algorithm;
    private readonly DiffHighlightOptions _defaultOptions;

    public MyersDiffAlgorithmTests()
    {
        _algorithm = new MyersDiffAlgorithm();
        _defaultOptions = new DiffHighlightOptions();
    }

    [Fact]
    public void ComputeDiff_IdenticalLines_ShouldReturnNoDifferences()
    {
        // Arrange
        var oldLines = new[] { "Hello World" };
        var newLines = new[] { "Hello World" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Type.Should().Be(EDiffLineType.Unchanged);
    }

    [Fact]
    public void ComputeDiff_CompletelyDifferentLines_ShouldReturnDeletionsAndAdditions()
    {
        // Arrange
        var oldLines = new[] { "ABC" };
        var newLines = new[] { "XYZ" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // 1 deletion + 1 addition
        
        var deletedLine = result.FirstOrDefault(d => d.Type == EDiffLineType.Deleted);
        var addedLine = result.FirstOrDefault(d => d.Type == EDiffLineType.Added);
        
        deletedLine.Should().NotBeNull();
        addedLine.Should().NotBeNull();
        deletedLine!.OldContent.Should().Be("ABC");
        addedLine!.NewContent.Should().Be("XYZ");
    }

    [Fact]
    public void ComputeDiff_SingleLineModification_ShouldDetectChange()
    {
        // Arrange
        var oldLines = new[] { "Original line" };
        var newLines = new[] { "Modified line" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // 1 deletion + 1 addition
        
        var deletedLine = result.FirstOrDefault(d => d.Type == EDiffLineType.Deleted);
        var addedLine = result.FirstOrDefault(d => d.Type == EDiffLineType.Added);
        
        deletedLine.Should().NotBeNull();
        addedLine.Should().NotBeNull();
        deletedLine!.OldContent.Should().Be("Original line");
        addedLine!.NewContent.Should().Be("Modified line");
    }

    [Fact]
    public void ComputeDiff_AdditionAtEnd_ShouldDetectAddition()
    {
        // Arrange
        var oldLines = new[] { "Line 1", "Line 2" };
        var newLines = new[] { "Line 1", "Line 2", "Line 3" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // 2 unchanged + 1 addition
        
        var addedLine = result.LastOrDefault(d => d.Type == EDiffLineType.Added);
        addedLine.Should().NotBeNull();
        addedLine!.NewContent.Should().Be("Line 3");
    }

    [Fact]
    public void ComputeDiff_DeletionAtEnd_ShouldDetectDeletion()
    {
        // Arrange
        var oldLines = new[] { "Line 1", "Line 2", "Line 3" };
        var newLines = new[] { "Line 1", "Line 2" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // 2 unchanged + 1 deletion
        
        var deletedLine = result.LastOrDefault(d => d.Type == EDiffLineType.Deleted);
        deletedLine.Should().NotBeNull();
        deletedLine!.OldContent.Should().Be("Line 3");
    }

    [Fact]
    public void ComputeDiff_MultipleChanges_ShouldDetectAllChanges()
    {
        // Arrange
        var oldLines = new[] { "Line 1", "Line 2", "Line 3" };
        var newLines = new[] { "Line 1", "Modified Line 2", "Line 3", "Line 4" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5); // 2 unchanged + 1 deletion + 1 addition + 1 addition
        
        var changes = result.Where(d => d.Type != EDiffLineType.Unchanged).ToList();
        changes.Should().HaveCount(3); // 1 deletion + 2 additions
    }

    [Fact]
    public void ComputeDiff_EmptyArrays_ShouldReturnEmpty()
    {
        // Arrange
        var oldLines = Array.Empty<string>();
        var newLines = Array.Empty<string>();

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_EmptyToNonEmpty_ShouldReturnAllAdditions()
    {
        // Arrange
        var oldLines = Array.Empty<string>();
        var newLines = new[] { "Line 1", "Line 2" };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.Type.Should().Be(EDiffLineType.Added));
    }

    [Fact]
    public void ComputeDiff_NonEmptyToEmpty_ShouldReturnAllDeletions()
    {
        // Arrange
        var oldLines = new[] { "Line 1", "Line 2" };
        var newLines = Array.Empty<string>();

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.Type.Should().Be(EDiffLineType.Deleted));
    }

    [Fact]
    public void ComputeCharacterDiff_ShouldDetectCharacterLevelChanges()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "Hello Universe";

        // Act
        var result = _algorithm.ComputeCharacterDiff(oldText, newText, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Should detect the change from "World" to "Universe"
        var deletedChars = result.Where(d => d.Type == EDiffLineType.Deleted).ToList();
        var addedChars = result.Where(d => d.Type == EDiffLineType.Added).ToList();
        
        deletedChars.Should().NotBeEmpty();
        addedChars.Should().NotBeEmpty();
    }

    [Fact]
    public void ComputeCharacterDiff_IdenticalText_ShouldReturnNoChanges()
    {
        // Arrange
        var text = "Hello World";

        // Act
        var result = _algorithm.ComputeCharacterDiff(text, text, _defaultOptions);

        // Assert
        result.Should().NotBeNull();
        result.Where(d => d.Type != EDiffLineType.Unchanged).Should().BeEmpty();
    }

    [Theory]
    [InlineData(new[] { "A" }, new[] { "B" }, 2)] // 1 deletion + 1 addition
    [InlineData(new[] { "A", "B" }, new[] { "A" }, 2)] // 1 unchanged + 1 deletion
    [InlineData(new[] { "A" }, new[] { "A", "B" }, 2)] // 1 unchanged + 1 addition
    public void ComputeDiff_VariousScenarios_ShouldReturnExpectedCount(string[] oldLines, string[] newLines, int expectedMinCount)
    {
        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, _defaultOptions);

        // Assert
        result.Should().HaveCount(expectedMinCount);
    }

    [Fact]
    public void ComputeDiff_WithIgnoreCaseOption_ShouldIgnoreCase()
    {
        // Arrange
        var oldLines = new[] { "Hello World" };
        var newLines = new[] { "HELLO WORLD" };
        var options = new DiffHighlightOptions { IgnoreCase = true };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, options);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Type.Should().Be(EDiffLineType.Unchanged);
    }

    [Fact]
    public void ComputeDiff_WithIgnoreWhitespaceOption_ShouldIgnoreWhitespace()
    {
        // Arrange
        var oldLines = new[] { "Hello World" };
        var newLines = new[] { "Hello  World" }; // Extra space
        var options = new DiffHighlightOptions { IgnoreWhitespace = true };

        // Act
        var result = _algorithm.ComputeDiff(oldLines, newLines, options);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Type.Should().Be(EDiffLineType.Unchanged);
    }
}