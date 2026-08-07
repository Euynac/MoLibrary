using AwesomeAssertions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed class ModuleConfigurationEfCoreTests
{
    [Fact]
    public void ModuleKey_WhenProjectedFromModuleType_ShouldBeDiagnosticOnly()
    {
        var key = ModuleKey.FromModuleType(typeof(ModuleConfigurationEfCore));

        key.Value.Should().Be(typeof(ModuleConfigurationEfCore).FullName);
    }
}
