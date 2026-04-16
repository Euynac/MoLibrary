using AwesomeAssertions;
using Monica.Core.Modularity.Models.Internal;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public class ModuleRegistrationStateTests
{
    [Fact]
    public void GetMissingRequiredConfigMethodKeys_WhenRequestUsesCascadedSuffix_ShouldTreatRequirementAsSatisfied()
    {
        var state = new ModuleRegistrationState(typeof(ModuleSystem))
        {
            RequiredConfigMethodKeys =
            [
                "ConfigDomainInfoProvider",
                "ConfigHttpClientRegisterProvider"
            ]
        };
        state.RegisterRequests.Add(new ModuleConfigurationRequest("ConfigDomainInfoProvider"));
        state.RegisterRequests.Add(new ModuleConfigurationRequest("ConfigHttpClientRegisterProvider_BuiltInModuleKey.DaprRpcClient"));

        state.GetMissingRequiredConfigMethodKeys().Should().BeEmpty();
    }

    [Fact]
    public void GetMissingRequiredConfigMethodKeys_WhenRequiredKeyIsAbsent_ShouldStillReportIt()
    {
        var state = new ModuleRegistrationState(typeof(ModuleSystem))
        {
            RequiredConfigMethodKeys =
            [
                "ConfigDomainInfoProvider",
                "ConfigHttpClientRegisterProvider"
            ]
        };
        state.RegisterRequests.Add(new ModuleConfigurationRequest("ConfigDomainInfoProvider"));

        state.GetMissingRequiredConfigMethodKeys().Should().BeEquivalentTo("ConfigHttpClientRegisterProvider");
    }
}
