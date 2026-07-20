using AwesomeAssertions;
using Monica.Core;
using Monica.Core.Modularity.Models.Internal;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public class ModuleRegistrationStateTests
{
    [Fact]
    public void GetMissingRequiredConfigMethodKeys_WhenRequestUsesCascadedSuffix_ShouldTreatRequirementAsSatisfied()
    {
        using var application = new MonicaApplication();
        var state = new ModuleRegistrationState(application, typeof(ModuleSystem))
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
        using var application = new MonicaApplication();
        var state = new ModuleRegistrationState(application, typeof(ModuleSystem))
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
