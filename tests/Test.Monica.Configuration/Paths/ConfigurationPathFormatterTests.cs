using AwesomeAssertions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Utils;
using Xunit;

namespace Test.Monica.Configuration.Paths;

public class ConfigurationPathFormatterTests
{
    [Fact]
    public void Format_WhenPropertyContainsSpecialCharacters_ShouldEscapeThem()
    {
        var path = new LogicalPath([new PropertySegment(@"Rag.Options[main]\Name")]);

        var formatted = ConfigurationPathFormatter.Format(path);

        formatted.Should().Be(@"Rag\.Options\[main\]\\Name");
        ConfigurationPathFormatter.Parse(formatted).Should().Be(path);
    }

    [Fact]
    public void Parse_WhenBracketSegmentUsesTypePrefixes_ShouldRestoreExactSegmentTypes()
    {
        var parsed = ConfigurationPathFormatter.Parse(@"Services[$123][#123][@123]");

        parsed.Segments.Should().Equal(
            new PropertySegment("Services"),
            new DictionaryKeySegment("123"),
            new ListItemKeySegment("123"),
            new ListIndexSegment(123));
    }

    [Fact]
    public void Parse_WhenBracketSegmentHasNoPrefix_ShouldThrowPathFormatException()
    {
        var act = () => ConfigurationPathFormatter.Parse("Services[123]");

        act.Should().Throw<ConfigurationPathFormatException>();
    }

    [Fact]
    public void Parse_WhenBracketIsUnterminated_ShouldThrowPathFormatException()
    {
        var act = () => ConfigurationPathFormatter.Parse("Services[$billing");

        act.Should().Throw<ConfigurationPathFormatException>();
    }
}
