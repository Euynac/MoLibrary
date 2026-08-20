using Monica.Tool.Units;

namespace Test.Monica.Tool.Units;

public sealed class UnitContractTests
{
    [Fact]
    public void Convert_WhenLengthValueIsProvided_ShouldConvertAcrossLinearUnits()
    {
        var converter = new LengthUnitConverter();

        var converted = converter.Convert("1km", "m");

        converted.Should().NotBeNull();
        converted!.Value.Should().BeApproximately(1000, 1e-10);
        converted.Unit.Symbol.Should().Be("m");
    }

    [Fact]
    public void Convert_WhenTemperatureValueIsProvided_ShouldConvertAcrossAffineUnits()
    {
        var converter = new TemperatureUnitConverter();

        converter.Convert("32℉", "℃")!.Value.Should().BeApproximately(0, 1e-10);
        converter.Convert("0℃", "K")!.Value.Should().BeApproximately(273.15, 1e-10);
    }

    [Fact]
    public void InformationUnits_WhenUsingNewestSiPrefixes_ShouldHaveCorrectScale()
    {
        var converter = new InformationUnitConverter();

        var ronnabytes = converter.Convert("1RB", "YB")!;
        var quettabytes = converter.Convert("1QB", "YB")!;

        ronnabytes.Value.Should().BeApproximately(1000, 1e-6);
        quettabytes.Value.Should().BeApproximately(1_000_000, 1e-3);
    }

    [Fact]
    public void ConvertToUnit_WhenDimensionsDiffer_ShouldRejectConversion()
    {
        var metre = new LengthUnitConverter().Units.Single(unit => unit.Symbol == "m");
        var celsius = new TemperatureUnitConverter().Units.Single(unit => unit.Symbol == "℃");

        var act = () => metre.ConvertToUnit(1, celsius);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConvertToUnit_WhenConvertersAreSeparateInstancesOfSameDimension_ShouldConvert()
    {
        var metre = new LengthUnitConverter().Units.Single(unit => unit.Symbol == "m");
        var kilometre = new LengthUnitConverter().Units.Single(unit => unit.Symbol == "km");

        metre.ConvertToUnit(1000, kilometre).Value.Should().BeApproximately(1, 1e-10);
    }

    [Fact]
    public void TryGetUnit_WhenSymbolsShareSuffix_ShouldPreferLongestSymbol()
    {
        var converter = new LengthUnitConverter();

        converter.TryGetUnit("1mm", out Unit? unit, out double value).Should().BeTrue();
        unit!.Symbol.Should().Be("mm");
        value.Should().Be(1);
    }

    [Fact]
    public void Regex_WhenCallerReplacesPublicPattern_ShouldRefreshParser()
    {
        var converter = new LengthUnitConverter { Regex = "(?<unit>mm)$" };

        converter.ContainsCurUnit("1m").Should().BeFalse();
        converter.TryGetUnit("1mm", out Unit? unit, out double value).Should().BeTrue();
        unit!.Symbol.Should().Be("mm");
        value.Should().Be(1);
    }

    [Fact]
    public void TryGetUnit_WhenSymbolContainsRegexCharacters_ShouldMatchLiterally()
    {
        var converter = new LengthUnitConverter();

        converter.TryGetUnit("1A.U.", out Unit? unit, out double value).Should().BeTrue();
        unit.Should().NotBeNull();
        unit!.Symbol.Should().Be("A.U.");
        value.Should().Be(1);
        converter.TryGetUnit("1AxUx", out _, out double _).Should().BeFalse();
    }
}
