using AwesomeAssertions;
using Monica.Core.Modularity.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleKeyTests
{
    [Fact]
    public void Create_WhenKeyFollowsEcosystemFormat_ShouldPreservePublishedIdentity()
    {
        var key = ModuleKey.Create("Acme.Monica.GachaPool.DrawHistory");

        key.Value.Should().Be("Acme.Monica.GachaPool.DrawHistory");
        key.IsBuiltIn.Should().BeFalse();
        key.IsUIModule.Should().BeFalse();
    }

    [Theory]
    [InlineData("Acme.Monica.GachaPool.UI", true)]
    [InlineData("Acme.Monica.GachaPool.ui", true)]
    [InlineData("Acme.Monica.GachaPoolUI", false)]
    [InlineData("Acme.Monica.UI.GachaPool", false)]
    public void Create_WhenKeyVariesUIPlacement_ShouldRecognizeOnlyFinalUISegment(string value, bool expected)
    {
        var key = ModuleKey.Create(value);

        key.IsUIModule.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Monica.Configuration.EfCore")]
    [InlineData("Acme.Monica")]
    [InlineData("Acme.Other.GachaPool")]
    [InlineData("Acme.monica.GachaPool")]
    [InlineData("Acme.Monica.Gacha-Pool")]
    [InlineData("Acme_Monica_GachaPool")]
    [InlineData("Acme.Monica.1GachaPool")]
    [InlineData("Acme..Monica.GachaPool")]
    public void Create_WhenKeyViolatesEcosystemFormat_ShouldThrow(string value)
    {
        var act = () => ModuleKey.Create(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenKeyExceedsNuGetIdentifierLimit_ShouldThrow()
    {
        var value = $"Acme.Monica.{new string('A', 89)}";

        var act = () => ModuleKey.Create(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_WhenCasingDiffers_ShouldUseOrdinalIgnoreCaseIdentity()
    {
        var canonical = ModuleKey.Create("Acme.Monica.GachaPool.UI");
        var differentlyCased = ModuleKey.Create("acme.Monica.gachapool.ui");
        var modules = new Dictionary<ModuleKey, string>
        {
            [canonical] = "registered"
        };

        differentlyCased.Should().Be(canonical);
        (differentlyCased == canonical).Should().BeTrue();
        differentlyCased.GetHashCode().Should().Be(canonical.GetHashCode());
        modules.Should().ContainKey(differentlyCased);
    }

    [Fact]
    public void BuiltInConversion_WhenConfigurationEfCoreIsUsed_ShouldCreateOfficialIdentity()
    {
        ModuleKey key = BuiltInModuleKey.ConfigurationEfCore;

        key.Value.Should().Be(nameof(BuiltInModuleKey.ConfigurationEfCore));
        key.IsBuiltIn.Should().BeTrue();
        key.IsUIModule.Should().BeFalse();
    }

    [Fact]
    public void DefaultValue_ShouldExposeAnEmptyNonNullString()
    {
        var key = default(ModuleKey);

        key.Value.Should().BeEmpty();
        key.ToString().Should().BeEmpty();
        ((string)key).Should().BeEmpty();
    }
}
