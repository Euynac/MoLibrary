using AwesomeAssertions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.Models;

public sealed class ConfigurationUnifiedVersionApplyPreviewTests
{
    [Fact]
    public void MissingDefinition_targets_are_skipped_and_do_not_block_the_apply()
    {
        var preview = CreatePreview(
            CreateTarget("Definition.Missing", ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition),
            CreateTarget("Definition.Ready", ConfigurationUnifiedVersionApplyTargetStatus.Ready),
            CreateTarget("Definition.Unchanged", ConfigurationUnifiedVersionApplyTargetStatus.Unchanged));

        preview.SkippedDefinitionKeys.Should().Equal("Definition.Missing");
        preview.SkippedTargets.Should().ContainSingle().Which.IsSkipped.Should().BeTrue();
        preview.ChangeCount.Should().Be(1);
        preview.BlockedCount.Should().Be(0);
        preview.CanApply.Should().BeTrue();
        preview.ChangedTargets.Select(static target => target.DefinitionKey)
            .Should().Equal("Definition.Ready");
    }

    [Fact]
    public void Preview_with_only_skipped_and_unchanged_targets_cannot_be_applied()
    {
        var preview = CreatePreview(
            CreateTarget("Definition.Missing", ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition),
            CreateTarget("Definition.Unchanged", ConfigurationUnifiedVersionApplyTargetStatus.Unchanged));

        preview.HasChanges.Should().BeFalse();
        preview.CanApply.Should().BeFalse();
    }

    [Fact]
    public void MissingDefinition_targets_do_not_mask_genuinely_blocked_targets()
    {
        var preview = CreatePreview(
            CreateTarget("Definition.Missing", ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition),
            CreateTarget("Definition.Invalid", ConfigurationUnifiedVersionApplyTargetStatus.InvalidValue));

        preview.SkippedCount.Should().Be(1);
        preview.BlockedCount.Should().Be(1);
        preview.CanApply.Should().BeFalse();
    }

    private static ConfigurationUnifiedVersionApplyPreview CreatePreview(
        params ConfigurationUnifiedVersionApplyTarget[] targets)
    {
        return new ConfigurationUnifiedVersionApplyPreview
        {
            Version = 1,
            PreviewFingerprint = "sha256:test",
            Targets = targets
        };
    }

    private static ConfigurationUnifiedVersionApplyTarget CreateTarget(
        string definitionKey,
        ConfigurationUnifiedVersionApplyTargetStatus status)
    {
        return new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = definitionKey,
            DisplayName = definitionKey,
            TargetJson = "{}",
            CapturedSchemaHash = "hash",
            CurrentSchemaHash = "hash",
            CurrentSchemaVersion = 1,
            Status = status
        };
    }
}
