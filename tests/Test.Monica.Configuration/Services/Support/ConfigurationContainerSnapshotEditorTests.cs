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
            LogicalPath.FromProperties("Services"),
            LogicalPath.FromProperties("Services", "Nested", "Enabled"),
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
            LogicalPath.FromProperties("Services"),
            LogicalPath.FromProperties("Services", "Nested", "Enabled"));

        patched.PlainJson.Should().Be("""{"Name":"billing","Nested":{}}""");
    }

    [Fact]
    public void Patch_WhenRelativePathIsEmpty_ShouldThrowValidationException()
    {
        var editor = new ConfigurationContainerSnapshotEditor();

        var act = () => editor.Patch(
            ConfigurationStoredValue.Plain("""{"Name":"billing"}"""),
            LogicalPath.FromProperties("Services"),
            LogicalPath.FromProperties("Services"),
            ConfigurationStoredValue.Plain("\"payments\""));

        act.Should().Throw<ConfigurationValidationFailedException>();
    }
}
