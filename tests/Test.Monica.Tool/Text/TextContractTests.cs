using System.Text;
using Monica.Tool.Annotations;
using Monica.Tool.Text;

namespace Test.Monica.Tool.Text;

public sealed class TextContractTests
{
    [Theory]
    [InlineData(0L, "0B")]
    [InlineData(1024L, "1KB")]
    [InlineData(1_572_864L, "1.5MB")]
    [InlineData(-1024L, "-1KB")]
    public void FormatByteSize_WhenValueIsProvided_ShouldChooseBinaryDisplayUnit(long value, string expected)
    {
        value.FormatByteSize().Should().Be(expected);
    }

    [Fact]
    public void FormatProgressBar_WhenPercentageIsOutsideRange_ShouldClampIt()
    {
        DisplayFormatting.FormatProgressBar(6, -1).Should().Be("[    ]");
        DisplayFormatting.FormatProgressBar(6, 2).Should().Be("[||||]");
    }

    [Fact]
    public void FormatProgressBar_WhenPercentageIsNotFinite_ShouldRejectIt()
    {
        var act = () => DisplayFormatting.FormatProgressBar(6, double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryParseAlias_WhenAliasExists_ShouldResolveEnumValue()
    {
        "primary".TryParseAlias<AliasedValue>(out var value).Should().BeTrue();
        value.Should().Be(AliasedValue.First);
    }

    [Fact]
    public void TryParseAliasFuzzy_WhenSearchIsEmpty_ShouldNotMatchEveryValue()
    {
        string.Empty.TryParseAliasFuzzy<AliasedValue>(out var values).Should().BeFalse();
        values.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEscapeChars_WhenEscapeIsDangling_ShouldReportMalformedInput()
    {
        var act = () => TextEscaper.RemoveEscapeChars("value\\");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void WidthConversion_WhenInputContainsControlCharacters_ShouldPreserveThem()
    {
        TextOperations.ToFullWidth("A\nB").Should().Be("Ａ\nＢ");
        TextOperations.ToHalfWidth("Ａ\nＢ").Should().Be("A\nB");
    }

    [Fact]
    public void ContainsChinese_WhenCharacterIsInFullUnifiedIdeographBlock_ShouldReturnTrue()
    {
        TextOperations.ContainsChinese("龘").Should().BeTrue();
    }

    [Fact]
    public void UnicodeConversions_WhenTextContainsSupplementaryRune_ShouldRoundTrip()
    {
        const string value = "Monica 😀 中文";

        TextOperations.UnicodeDecode(TextOperations.UnicodeEncode(value)).Should().Be(value);
        TextOperations.StringDecode(TextOperations.StringEncode(value, Encoding.Unicode), Encoding.Unicode)
            .Should().Be(value);
    }

    [Fact]
    public void PunctuationConversion_WhenEllipsisAndQuotesArePresent_ShouldAvoidCascadingReplacements()
    {
        const string english = "Wait... \"okay\".";

        var chinese = english.ToZhPunctuation();

        chinese.Should().Be("Wait… “okay”。");
        chinese.ToEnPunctuation().Should().Be(english);
    }

    [Theory]
    [InlineData(" YES ", true)]
    [InlineData("Off", false)]
    [InlineData("正确", true)]
    public void TryToBool_WhenLiteralHasSupportedCasingOrWhitespace_ShouldParse(string input, bool expected)
    {
        TextValueParser.TryToBool(input, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void TryToEnum_WhenRuntimeTypeIsNotEnum_ShouldReturnFalse()
    {
        var act = () => TextValueParser.TryToEnum("1", typeof(int), out _);

        act.Should().NotThrow();
        TextValueParser.TryToEnum("1", typeof(int), out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetTimeSpanInterval_WhenFractionsUseTicks_ShouldPreservePrecision()
    {
        "00:00:01.1234567 - 00:00:02.7654321".TryGetTimeSpanInterval(out var left, out var right)
            .Should().BeTrue();
        left.Should().Be(TimeSpan.FromTicks(11_234_567));
        right.Should().Be(TimeSpan.FromTicks(27_654_321));
    }

    [Fact]
    public void TryGetTimeSpanInterval_WhenInputIsPathological_ShouldReturnFalseWithDefaultOutputs()
    {
        var input = new string('9', 100_000) + " - 1";

        input.TryGetTimeSpanInterval(out var left, out var right).Should().BeFalse();
        left.Should().Be(default);
        right.Should().Be(default);
    }

    [Fact]
    public void DetectLang_WhenSnippetIsCSharp_ShouldReturnCSharp()
    {
        var detector = new LangDetector();
        const string snippet = "using System;\nnamespace Sample;\npublic sealed class Widget { }";

        detector.DetectLang(snippet).Should().Be(LangDetector.SupportedLanguages.CSharp);
    }

    [Fact]
    public void DetectLang_WhenSnippetIsEmpty_ShouldReturnUnknown()
    {
        new LangDetector().DetectLang(string.Empty).Should().Be(LangDetector.SupportedLanguages.Unknown);
    }

    [Fact]
    public void DetectLang_WhenCodeFollowsManyBlankLines_ShouldStillInspectTheCode()
    {
        var snippet = new string('\n', 601) + "using System; namespace Sample; public sealed class Widget { }";

        new LangDetector().DetectLang(snippet).Should().Be(LangDetector.SupportedLanguages.CSharp);
    }

    private enum AliasedValue
    {
        [EnumAlias("primary", "first")]
        First,
        Second,
    }
}
