using AwesomeAssertions;
using Monica.Configuration.Serialization;
using Xunit;

namespace Test.Monica.Configuration.Serialization;

public class ConfigurationDefinitionSchemaCodecTests
{
    [Fact]
    public void ToCompactClrTypeName_WhenTypeIsAssemblyQualifiedGeneric_ShouldUseCleanFullName()
    {
        var clrTypeName = typeof(Dictionary<string, List<int>>).AssemblyQualifiedName!;

        var compact = ConfigurationDefinitionSchemaCodec.ToCompactClrTypeName(clrTypeName);

        compact.Should().Be("System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>");
        compact.Should().NotContain("Version=");
        compact.Should().NotContain("PublicKeyToken");
    }
}
