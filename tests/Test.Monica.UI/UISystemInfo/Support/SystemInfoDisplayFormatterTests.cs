using System.Globalization;
using AwesomeAssertions;
using Monica.UI.UISystemInfo.Support;
using Xunit;

namespace Test.Monica.UI.UISystemInfo.Support;

public sealed class SystemInfoDisplayFormatterTests
{
    [Theory]
    [InlineData(-1, "00:00")]
    [InlineData(62, "01:02")]
    [InlineData(3723, "01:02:03")]
    [InlineData(183845, "2.03:04:05")]
    public void FormatDuration_ShouldClampAndSelectTheCompactOperationalShape(
        double seconds,
        string expected)
    {
        SystemInfoDisplayFormatter.FormatDuration(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void FormatScaledBytes_ShouldUseBinaryUnitsAndClampNegativeValues()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        SystemInfoDisplayFormatter.FormatScaledBytes(1_572_864, culture).Should().Be("1.5 MiB");
        SystemInfoDisplayFormatter.FormatScaledBytes(-1, culture).Should().Be("0 B");
        SystemInfoDisplayFormatter.FormatByteCount(-1, culture).Should().Be("0 B");
    }

}
