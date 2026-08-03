using AwesomeAssertions;
using Monica.Modules;

namespace Test.Monica.Configuration.UI.Modules;

public sealed class ModuleConfigurationUIOptionTests
{
    [Fact]
    public void EnableAffectedServiceConfirmation_WhenNotConfigured_ShouldBeDisabled()
    {
        var option = new ModuleConfigurationUIOption();

        option.EnableAffectedServiceConfirmation.Should().BeFalse();
    }
}
