using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Utils;
using Xunit;

namespace Test.Monica.Configuration.Paths;

public class ConfigurationDictionaryKeyEscaperTests
{
    [Fact]
    public void ThrowIfInvalidForProjection_WhenKeyContainsColon_ShouldRejectIt()
    {
        var act = () => ConfigurationDictionaryKeyEscaper.ThrowIfInvalidForProjection("tenant:billing");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToCanonicalString_WhenDictionaryKeyContainsDisplaySpecialCharacters_ShouldRoundTrip()
    {
        var path = new LogicalPath([new PropertySegment("Services"), new DictionaryKeySegment("a.b[c]\"q\"\\x")]);

        var canonical = path.ToCanonicalString();

        canonical.Should().Be(@"Services[$a\.b\[c\]""q""\\x]");
        LogicalPath.Parse(canonical).Should().Be(path);
    }
}
