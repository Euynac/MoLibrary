using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationEffectiveValueDocumentEditorTests
{
    [Fact]
    public void ApplyMutation_WhenPathTargetsKeyedListItem_ShouldPatchDocument()
    {
        var editor = new ConfigurationEffectiveValueDocumentEditor(
            new ConfigurationContainerSnapshotEditor(),
            new ConfigurationStoredValueCodec());
        var definition = TestConfigurationFactory.Definition();
        var path = TestConfigurationFactory.ConnectedDbPath("billing", "main")
            .Append(new PropertySegment("ConnectionString"));
        const string json = """
                            {
                              "ServiceMap": {
                                "billing": {
                                  "ConnectedDbs": [
                                    {
                                      "Name": "main",
                                      "ConnectionString": "old"
                                    }
                                  ]
                                }
                              }
                            }
                            """;

        var updated = editor.ApplyMutation(definition, json, new ConfigurationMutationRequest
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = path,
            MutationKind = ConfigurationMutationKind.Set,
            Value = ConfigurationStoredValue.Plain("\"new\""),
            ExpectedSchemaVersion = definition.SchemaVersion
        });

        var value = editor.ReadValue(definition, updated, path);
        new ConfigurationStoredValueCodec().ToConfigurationString(value!).Should().Be("new");
    }

    [Fact]
    public void Project_WhenDocumentHasScalarsAndArrays_ShouldEmitMicrosoftConfigurationKeys()
    {
        var editor = new ConfigurationEffectiveValueDocumentEditor(
            new ConfigurationContainerSnapshotEditor(),
            new ConfigurationStoredValueCodec());
        var definition = TestConfigurationFactory.Definition();

        var projected = editor.Project(definition, """
                                                  {
                                                    "WorkerId": 7,
                                                    "Services": [
                                                      {
                                                        "Name": "billing"
                                                      }
                                                    ]
                                                  }
                                                  """);

        projected["Test:App:WorkerId"].Should().Be("7");
        projected["Test:App:Services:0:Name"].Should().Be("billing");
    }
}
