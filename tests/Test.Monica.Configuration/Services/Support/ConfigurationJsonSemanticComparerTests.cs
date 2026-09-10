using AwesomeAssertions;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public sealed class ConfigurationJsonSemanticComparerTests
{
    [Fact]
    public void Equals_WhenOneSideKeepsExplicitNullsAndTheOtherOmitsThem_ShouldBeEqual()
    {
        var stored = """
            {"Name":"alpha","Tag":null}
            """;
        var runtime = """
            {"Name":"alpha"}
            """;

        ConfigurationJsonSemanticComparer.Equals(stored, runtime).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenBothSidesKeepExplicitNulls_ShouldBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"Name":"alpha","Tag":null}""",
                """{"Tag":null,"Name":"alpha"}""")
            .Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenNullIsComparedWithNonNullValue_ShouldNotBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"Name":"alpha","Tag":null}""",
                """{"Name":"alpha","Tag":"keep"}""")
            .Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenListItemsDifferOnlyByExplicitNulls_ShouldBeEqual()
    {
        var stored = """
            {"Items":[{"Name":"a","Tag":null},{"Name":"b"}]}
            """;
        var runtime = """
            {"Items":[{"Name":"a"},{"Name":"b"}]}
            """;

        ConfigurationJsonSemanticComparer.Equals(stored, runtime).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenPropertyIsMissingOnBothSides_ShouldBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"A":1}""",
                """{"A":1}""")
            .Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenPropertyValuesDiffer_ShouldNotBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"A":1}""",
                """{"A":2}""")
            .Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenPropertyNamesDifferOnlyInCase_ShouldMatchProperties()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"workerId":1}""",
                """{"WorkerId":1}""")
            .Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenNumbersHaveDifferentTextualForms_ShouldBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"Value":1.0}""",
                """{"Value":1.00}""")
            .Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenObjectsContainDuplicateCaseInsensitiveProperties_ShouldNotBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"A":1,"a":2}""",
                """{"A":1}""")
            .Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenExtraNonNullPropertyExistsOnOneSide_ShouldNotBeEqual()
    {
        ConfigurationJsonSemanticComparer.Equals(
                """{"A":1,"B":2}""",
                """{"A":1}""")
            .Should().BeFalse();
    }
}
