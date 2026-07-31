using AwesomeAssertions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public class ConfigurationUnifiedVersionRollbackPreviewFingerprintTests
{
    [Fact]
    public void Compute_WhenPreviewIsUnchanged_ShouldReturnStableFingerprint()
    {
        var target = CreateTarget();

        var first = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(7, [target]);
        var second = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(7, [target]);

        first.Should().Be(second);
        first.Should().StartWith("sha256:");
    }

    [Fact]
    public void Compute_WhenConcurrencyRevisionChanges_ShouldReturnDifferentFingerprint()
    {
        var target = CreateTarget();
        var changedTarget = target with
        {
            Mutations =
            [
                target.Mutations[0] with { ExpectedSourceChainRevision = "source-chain:2" }
            ]
        };

        var original = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(7, [target]);
        var changed = ConfigurationUnifiedVersionRollbackPreviewFingerprint.Compute(7, [changedTarget]);

        changed.Should().NotBe(original);
    }

    private static ConfigurationUnifiedVersionApplyTarget CreateTarget()
    {
        return new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = "Test.Options",
            DisplayName = "Test options",
            CurrentJson = "{\"Enabled\":false}",
            TargetJson = "{\"Enabled\":true}",
            CapturedSchemaHash = "schema:1",
            CurrentSchemaHash = "schema:1",
            CurrentSchemaVersion = 3,
            Mutations =
            [
                new ConfigurationUnifiedVersionApplyMutation
                {
                    LogicalPath = "$.Enabled",
                    ConfigurationPath = "Test:Enabled",
                    MutationKind = ConfigurationMutationKind.Set,
                    CurrentJson = "false",
                    TargetJson = "true",
                    SourceKey = "db:default",
                    SourceKind = ConfigurationSourceKind.MonicaEffectiveStore,
                    ExpectedValueVersion = 5,
                    ExpectedSourceChainRevision = "source-chain:1"
                }
            ]
        };
    }
}
