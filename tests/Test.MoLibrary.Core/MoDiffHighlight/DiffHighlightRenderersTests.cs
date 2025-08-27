using FluentAssertions;
using MoLibrary.Core.Features.MoDiffHighlight;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Features.MoDiffHighlight.Renderers;
using Xunit;

namespace Test.MoLibrary.Core.MoDiffHighlight;

public class DiffHighlightRenderersTests
{
    private readonly List<DiffLine> _sampleLines = new()
    {
        new DiffLine { Type = EDiffLineType.Unchanged, OldContent = "Unchanged line", NewContent = "Unchanged line", OldLineNumber = 1, NewLineNumber = 1 },
        new DiffLine { Type = EDiffLineType.Deleted, OldContent = "Old content", NewContent = "", OldLineNumber = 2, NewLineNumber = 0 },
        new DiffLine { Type = EDiffLineType.Added, OldContent = "", NewContent = "New content", OldLineNumber = 0, NewLineNumber = 2 },
        new DiffLine { Type = EDiffLineType.Unchanged, OldContent = "Another unchanged", NewContent = "Another unchanged", OldLineNumber = 3, NewLineNumber = 3 }
    };

    [Fact]
    public void HtmlDiffRenderer_Render_ShouldGenerateValidHtml()
    {
        // Arrange
        var renderer = new HtmlDiffRenderer();
        var style = new DiffHighlightStyle();

        // Act
        var html = renderer.Render(_sampleLines, style);

        // Assert
        html.Should().NotBeNullOrEmpty();
        html.Should().Contain("html");
        html.Should().Contain("Unchanged line");
        html.Should().Contain("Old content");
        html.Should().Contain("New content");
        html.Should().Contain("Another unchanged");
    }

    [Fact]
    public void MarkdownDiffRenderer_Render_ShouldGenerateValidMarkdown()
    {
        // Arrange
        var renderer = new MarkdownDiffRenderer();
        var style = new DiffHighlightStyle();

        // Act
        var markdown = renderer.Render(_sampleLines, style);

        // Assert
        markdown.Should().NotBeNullOrEmpty();
        markdown.Should().Contain("Unchanged line");
        markdown.Should().Contain("Old content");
        markdown.Should().Contain("New content");
    }

    [Fact]
    public void PlainTextDiffRenderer_Render_ShouldGenerateValidPlainText()
    {
        // Arrange
        var renderer = new PlainTextDiffRenderer();
        var style = new DiffHighlightStyle();

        // Act
        var plainText = renderer.Render(_sampleLines, style);

        // Assert
        plainText.Should().NotBeNullOrEmpty();
        plainText.Should().Contain("Unchanged line");
        plainText.Should().Contain("Old content");
        plainText.Should().Contain("New content");
    }

    [Fact]
    public void AllRenderers_Render_ShouldHandleEmptyInput()
    {
        // Arrange
        var renderers = new IDiffHighlightRenderer[]
        {
            new HtmlDiffRenderer(),
            new MarkdownDiffRenderer(),
            new PlainTextDiffRenderer()
        };

        var emptyLines = new List<DiffLine>();
        var style = new DiffHighlightStyle();

        // Act & Assert
        foreach (var renderer in renderers)
        {
            var output = renderer.Render(emptyLines, style);
            output.Should().NotBeNull(); // Should not throw, may return empty or minimal content
        }
    }

    [Theory]
    [InlineData(typeof(HtmlDiffRenderer))]
    [InlineData(typeof(MarkdownDiffRenderer))]
    [InlineData(typeof(PlainTextDiffRenderer))]
    public void AllRenderers_ShouldImplementInterface(Type rendererType)
    {
        // Arrange & Act
        var instance = Activator.CreateInstance(rendererType);

        // Assert
        instance.Should().BeAssignableTo<IDiffHighlightRenderer>();
    }

    [Fact]
    public void HtmlDiffRenderer_ShouldHaveCorrectFormat()
    {
        // Arrange
        var renderer = new HtmlDiffRenderer();

        // Assert
        renderer.SupportedFormat.Should().Be(EDiffOutputFormat.Html);
    }

    [Fact]
    public void MarkdownDiffRenderer_ShouldHaveCorrectFormat()
    {
        // Arrange
        var renderer = new MarkdownDiffRenderer();

        // Assert
        renderer.SupportedFormat.Should().Be(EDiffOutputFormat.Markdown);
    }

    [Fact]
    public void PlainTextDiffRenderer_ShouldHaveCorrectFormat()
    {
        // Arrange
        var renderer = new PlainTextDiffRenderer();

        // Assert
        renderer.SupportedFormat.Should().Be(EDiffOutputFormat.PlainText);
    }
}