using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.MoDiffHighlight;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Modules;
using NSubstitute;
using Xunit;

namespace Test.MoLibrary.Core.MoDiffHighlight;

public class DiffHighlightServiceTests
{
    private readonly IMoDiffHighlight _mockDiffHighlight;
    private readonly IOptions<ModuleDiffHighlightOption> _mockOptions;
    private readonly ILogger<DiffHighlightService> _mockLogger;
    private readonly DiffHighlightService _service;

    public DiffHighlightServiceTests()
    {
        _mockDiffHighlight = Substitute.For<IMoDiffHighlight>();
        _mockOptions = Substitute.For<IOptions<ModuleDiffHighlightOption>>();
        _mockLogger = Substitute.For<ILogger<DiffHighlightService>>();

        _mockOptions.Value.Returns(new ModuleDiffHighlightOption());

        _service = new DiffHighlightService(_mockDiffHighlight, _mockLogger);
    }

    [Fact]
    public async Task HighlightAsync_WithValidInput_ShouldCallDiffHighlightAndReturnResult()
    {
        // Arrange
        var oldText = "Hello World";
        var newText = "Hello Universe";
        var expectedResult = new DiffHighlightResult
        {
            Statistics = new DiffStatistics { TotalChanges = 1 },
            HighlightedContent = "<div>Diff content</div>",
            Lines = new List<DiffLine>()
        };

        _mockDiffHighlight.HighlightAsync(oldText, newText, Arg.Any<DiffHighlightOptions>())
            .Returns(expectedResult);

        // Act
        var result = await _service.HighlightAsync(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedResult);
        await _mockDiffHighlight.Received(1).HighlightAsync(oldText, newText, Arg.Any<DiffHighlightOptions>());
    }

    [Fact]
    public void Highlight_SynchronousVersion_ShouldCallDiffHighlightSync()
    {
        // Arrange
        var oldText = "Hello";
        var newText = "Hi";
        var expectedResult = new DiffHighlightResult
        {
            Statistics = new DiffStatistics(),
            Lines = new List<DiffLine>()
        };

        _mockDiffHighlight.Highlight(oldText, newText, Arg.Any<DiffHighlightOptions>())
            .Returns(expectedResult);

        // Act
        var result = _service.Highlight(oldText, newText);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedResult);
        _mockDiffHighlight.Received(1).Highlight(oldText, newText, Arg.Any<DiffHighlightOptions>());
    }

    [Theory]
    [InlineData(null, "newText")]
    [InlineData("oldText", null)]
    [InlineData(null, null)]
    public async Task HighlightAsync_WithNullInput_ShouldThrowArgumentNullException(string? oldText, string? newText)
    {
        // Act & Assert
        var act = async () => await _service.HighlightAsync(oldText!, newText!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDependencies_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act1 = () => new DiffHighlightService(null!, _mockLogger);
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => new DiffHighlightService(_mockDiffHighlight, null!);
        act2.Should().Throw<ArgumentNullException>();
    }
}