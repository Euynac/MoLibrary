using AwesomeAssertions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationEffectiveValuePatchEngineTests
{
    [Fact]
    public void Patch_WhenNestedPropertyExists_ShouldUpdateOnlyTargetValue()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Patch(
            """{"Services":[{"Name":"billing","Nested":{"Enabled":false}}]}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("billing").Append(new PropertySegment("Nested")).Append(new PropertySegment("Enabled")),
            "true");

        patched.Should().Be("""{"Services":[{"Name":"billing","Nested":{"Enabled":true}}]}""");
    }

    [Fact]
    public void Remove_WhenNestedPropertyExists_ShouldRemoveOnlyTargetValue()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Remove(
            """{"Services":[{"Name":"billing","Nested":{"Enabled":true}}]}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("billing").Append(new PropertySegment("Nested")).Append(new PropertySegment("Enabled")));

        patched.Should().Be("""{"Services":[{"Name":"billing","Nested":{}}]}""");
    }

    [Fact]
    public void Patch_WhenTargetPathIsRoot_ShouldThrowValidationException()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var act = () => engine.Patch(
            """{"Services":[]}""",
            TestConfigurationFactory.Definition(),
            LogicalPath.Root,
            """{"Services":[]}""");

        act.Should().Throw<ConfigurationValidationFailedException>();
    }

    [Fact]
    public void Patch_WhenKeyedListContainsItem_ShouldPatchMatchingArrayItem()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Patch(
            """{"Services":[{"Name":"main","ConnectionString":"old"},{"Name":"logs","ConnectionString":"old"}]}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString")),
            "\"new\"");

        patched.Should().Be(
            """{"Services":[{"Name":"main","ConnectionString":"new"},{"Name":"logs","ConnectionString":"old"}]}""");
    }

    [Fact]
    public void Patch_WhenKeyedListDoesNotContainItem_ShouldAppendNewArrayItem()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Patch(
            """{"Services":[{"Name":"logs","ConnectionString":"old"}]}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString")),
            "\"new\"");

        patched.Should().Be(
            """{"Services":[{"Name":"logs","ConnectionString":"old"},{"Name":"main","ConnectionString":"new"}]}""");
    }

    [Fact]
    public void Remove_WhenKeyedListContainsItem_ShouldRemoveMatchingArrayItem()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Remove(
            """{"Services":[{"Name":"main","ConnectionString":"old"},{"Name":"logs","ConnectionString":"old"}]}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("main"));

        patched.Should().Be("""{"Services":[{"Name":"logs","ConnectionString":"old"}]}""");
    }

    [Fact]
    public void Patch_WhenDictionaryContainsNestedList_ShouldPatchDictionaryItem()
    {
        var engine = new ConfigurationEffectiveValuePatchEngine();

        var patched = engine.Patch(
            """{"ServiceMap":{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"old"}]}}}""",
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ConnectedDbPath("billing", "main").Append(new PropertySegment("ConnectionString")),
            "\"new\"");

        patched.Should().Be(
            """{"ServiceMap":{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"new"}]}}}""");
    }
}
