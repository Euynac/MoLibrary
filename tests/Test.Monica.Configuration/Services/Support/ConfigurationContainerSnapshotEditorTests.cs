using AwesomeAssertions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationContainerSnapshotEditorTests
{
    [Fact]
    public void Patch_WhenDescendantPropertyExists_ShouldUpdateOnlyTargetValue()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain("""{"Name":"billing","Nested":{"Enabled":false}}""");

        var patched = editor.Patch(
            container,
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("billing"),
            TestConfigurationFactory.ServiceItemPath("billing").Append(new PropertySegment("Nested")).Append(new PropertySegment("Enabled")),
            ConfigurationStoredValue.Plain("true"));

        patched.PlainJson.Should().Be("""{"Name":"billing","Nested":{"Enabled":true}}""");
    }

    [Fact]
    public void Remove_WhenDescendantPropertyExists_ShouldRemoveOnlyTargetValue()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain("""{"Name":"billing","Nested":{"Enabled":true}}""");

        var patched = editor.Remove(
            container,
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("billing"),
            TestConfigurationFactory.ServiceItemPath("billing").Append(new PropertySegment("Nested")).Append(new PropertySegment("Enabled")));

        patched.PlainJson.Should().Be("""{"Name":"billing","Nested":{}}""");
    }

    [Fact]
    public void Patch_WhenRelativePathIsEmpty_ShouldThrowValidationException()
    {
        var editor = new ConfigurationContainerSnapshotEditor();

        var act = () => editor.Patch(
            ConfigurationStoredValue.Plain("""{"Name":"billing"}"""),
            TestConfigurationFactory.Definition(),
            TestConfigurationFactory.ServiceItemPath("billing"),
            TestConfigurationFactory.ServiceItemPath("billing"),
            ConfigurationStoredValue.Plain("\"payments\""));

        act.Should().Throw<ConfigurationValidationFailedException>();
    }

    [Fact]
    public void Patch_WhenWholeListSnapshotUsesStableItemKey_ShouldPatchMatchingArrayItem()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain(
            """[{"Name":"main","ConnectionString":"old"},{"Name":"logs","ConnectionString":"old"}]""");

        var patched = editor.Patch(
            container,
            TestConfigurationFactory.Definition(),
            LogicalPath.FromProperties("Services"),
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString")),
            ConfigurationStoredValue.Plain("\"new\""));

        patched.PlainJson.Should().Be(
            """[{"Name":"main","ConnectionString":"new"},{"Name":"logs","ConnectionString":"old"}]""");
    }

    [Fact]
    public void Patch_WhenWholeListSnapshotDoesNotContainStableItemKey_ShouldAppendNewArrayItem()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain("""[{"Name":"logs","ConnectionString":"old"}]""");

        var patched = editor.Patch(
            container,
            TestConfigurationFactory.Definition(),
            LogicalPath.FromProperties("Services"),
            TestConfigurationFactory.ServiceItemPath("main").Append(new PropertySegment("ConnectionString")),
            ConfigurationStoredValue.Plain("\"new\""));

        patched.PlainJson.Should().Be(
            """[{"Name":"logs","ConnectionString":"old"},{"Name":"main","ConnectionString":"new"}]""");
    }

    [Fact]
    public void Remove_WhenWholeListSnapshotUsesStableItemKey_ShouldRemoveMatchingArrayItem()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain(
            """[{"Name":"main","ConnectionString":"old"},{"Name":"logs","ConnectionString":"old"}]""");

        var patched = editor.Remove(
            container,
            TestConfigurationFactory.Definition(),
            LogicalPath.FromProperties("Services"),
            TestConfigurationFactory.ServiceItemPath("main"));

        patched.PlainJson.Should().Be("""[{"Name":"logs","ConnectionString":"old"}]""");
    }

    [Fact]
    public void Patch_WhenDictionaryContainerSnapshotCoversDescendant_ShouldPatchDictionaryEntry()
    {
        var editor = new ConfigurationContainerSnapshotEditor();
        var container = ConfigurationStoredValue.Plain(
            """{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"old"}]}}""");

        var patched = editor.Patch(
            container,
            TestConfigurationFactory.Definition(),
            LogicalPath.FromProperties("ServiceMap"),
            TestConfigurationFactory.ConnectedDbPath("billing", "main").Append(new PropertySegment("ConnectionString")),
            ConfigurationStoredValue.Plain("\"new\""));

        patched.PlainJson.Should().Be(
            """{"billing":{"ConnectedDbs":[{"Name":"main","ConnectionString":"new"}]}}""");
    }
}
