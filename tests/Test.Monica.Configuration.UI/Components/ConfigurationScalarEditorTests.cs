using AwesomeAssertions;
using Bunit;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Components;
using Test.Monica.Configuration.UI.Infrastructure;

namespace Test.Monica.Configuration.UI.Components;

public sealed class ConfigurationScalarEditorTests
{
    [Fact]
    public async Task Render_WhenScalarStringNodeHasEditorHint_ShouldShowHintBadge()
    {
        await using var context = new ConfigurationUiTestContext();

        var editor = context.Render<ConfigurationScalarEditor>(parameters => parameters
            .Add(component => component.Node, CreateNode("Airway")));

        editor.Markup.Should().Contain("configuration-editor-hint-badge");
        editor.Markup.Should().Contain("Airway");
    }

    [Fact]
    public async Task Render_WhenNodeHasNoEditorHint_ShouldOmitHintBadge()
    {
        await using var context = new ConfigurationUiTestContext();

        var editor = context.Render<ConfigurationScalarEditor>(parameters => parameters
            .Add(component => component.Node, CreateNode(null)));

        editor.Markup.Should().NotContain("configuration-editor-hint-badge");
    }

    private static ConfigurationNodeDefinition CreateNode(string? editorHint)
    {
        var path = LogicalPath.FromProperties("Route");
        return new ConfigurationNodeDefinition
        {
            NodeKey = "Route",
            Name = "Route",
            DisplayName = "航路",
            RelativePath = path,
            ConfigurationPath = "EditorHintOptions:Route",
            ClrTypeName = typeof(string).AssemblyQualifiedName!,
            NodeKind = ConfigurationNodeKind.Scalar,
            ValueKind = ConfigurationValueKind.String,
            EditorHint = editorHint
        };
    }
}
