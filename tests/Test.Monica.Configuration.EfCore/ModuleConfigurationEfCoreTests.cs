using AwesomeAssertions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Configuration.EfCore;

public sealed class ModuleConfigurationEfCoreTests
{
    [Fact]
    public void ModuleKey_WhenInspected_ShouldIdentifyOfficialBuiltInModule()
    {
        var attribute = typeof(ModuleConfigurationEfCore)
            .GetCustomAttributes(typeof(ModuleKeyAttribute), inherit: false)
            .Cast<ModuleKeyAttribute>()
            .Single();

        attribute.Key.Should().Be((ModuleKey)BuiltInModuleKey.ConfigurationEfCore);
        attribute.Key.IsBuiltIn.Should().BeTrue();
    }
}
