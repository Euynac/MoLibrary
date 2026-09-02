using AwesomeAssertions;
using Monica.Guide;
using Xunit;

namespace Test.Monica.Guide;

public sealed class AgentProductPlatformTests
{
    public static TheoryData<AgentProductDefinition> Products =>
        new(KnownAgentProducts.All);

    [Theory]
    [MemberData(nameof(Products))]
    public void EveryProductPublishesThePortableThreePlatformMatrix(AgentProductDefinition product)
    {
        product.Platforms.Select(static platform => platform.RuntimeIdentifier)
            .Should().BeEquivalentTo(["win-x64", "linux-x64", "osx-arm64"], options => options.WithStrictOrdering());

        product.Platforms.Select(static platform => platform.DistributionKind)
            .Should().BeEquivalentTo(
                ["portable-win-x64", "portable-linux-x64", "portable-osx-arm64"],
                options => options.WithStrictOrdering());

        // Windows keeps the .exe suffix; Unix platforms ship the same name without it.
        product.Platforms[0].ExecutableName
            .Should().Be($"{product.Platforms[1].ExecutableName}.exe");
        product.Platforms[1].ExecutableName.Should().Be(product.Platforms[2].ExecutableName);
    }

    [Theory]
    [MemberData(nameof(Products))]
    public void ArchiveTemplatesCarryBothPlaceholders(AgentProductDefinition product)
    {
        product.ArchiveAssetNameTemplate
            .Should().Contain("{version}").And.Contain("{rid}")
            .And.EndWith("-{rid}.zip");
    }

    [Fact]
    public void ProgramEntryPointFor_UsesThePlatformSpecificExecutableName()
    {
        var workflow = KnownAgentProducts.MonicaWorkflow;

        workflow.ProgramEntryPointFor("win-x64").Should().Be("app/Monica.Workflow.exe");
        workflow.ProgramEntryPointFor("linux-x64").Should().Be("app/Monica.Workflow");
        workflow.ProgramEntryPointFor("osx-arm64").Should().Be("app/Monica.Workflow");
        workflow.ProgramEntryPointFor("linux-arm64").Should().BeNull();
    }

    [Fact]
    public void CurrentPlatform_ResolvesTheRunningHost()
    {
        var monica = KnownAgentProducts.Monica;

        monica.CurrentPlatform().Should().NotBeNull();
        monica.CurrentPlatform()!.RuntimeIdentifier.Should().Be(AgentProductPlatform.CurrentRuntimeIdentifier);
        // The Monica guide executable is the product's only program, so its bundle ships
        // it as the setup tree; serve products like Workflow keep the app/ tree.
        monica.CurrentProgramEntryPoint.Should().Be($"setup/{monica.CurrentPlatform()!.ExecutableName}");
    }
}
