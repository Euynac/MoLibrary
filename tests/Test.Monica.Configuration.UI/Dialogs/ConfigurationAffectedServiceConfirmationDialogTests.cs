using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Dialogs;
using MudBlazor;
using Test.Monica.Configuration.UI.Infrastructure;

namespace Test.Monica.Configuration.UI.Dialogs;

public sealed class ConfigurationAffectedServiceConfirmationDialogTests
{
    [Fact]
    public async Task Render_WhenImpactIsMixed_ShouldShowOnlineRestartUncertainAndUnknownParameterDetails()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, _) = await RenderDialogAsync(context, CreateMixedImpact(), changeCount: 4);

        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Summary [4, 2]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Online [1]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Restart [1]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Uncertain [2]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:RestartRequiredTitle [1]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UncertainImpactTitle");
        surface.Markup.Should().Contain("flight-service");
        surface.Markup.Should().Contain("message-service");
        surface.Markup.Should().Contain("AI module options");
        surface.Markup.Should().Contain("Git.Url");
        surface.Markup.Should().Contain("Git.Token");
        surface.Markup.Should().Contain("ReloadBehaviors:Labels:OnlineReloadable");
        surface.Markup.Should().Contain("ReloadBehaviors:Labels:RequiresRestart");
        surface.Markup.Should().Contain("ReloadBehaviors:Labels:Unknown");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:ConsumptionEvidence");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:EffectiveBehavior");
        surface.Markup.Should().Contain("DefinitionPublication:ReloadContributors:Evidence:Declared:Label");
        surface.Markup.Should().Contain("DefinitionPublication:ReloadContributors:Evidence:Inferred:Label");
        surface.Markup.Should().Contain("DefinitionPublication:ReloadContributors:Evidence:Unresolved:Label");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UnknownConsumersTitle [1]");
        surface.Markup.Should().Contain("Unclaimed endpoint");
        surface.Markup.Should().Contain("Endpoint");
        FindButton(surface, "Dialogs:SaveGroup:Impact:Actions:ConfirmSave").Should().NotBeNull();
        surface.FindAll("button")
            .Should().NotContain(button => button.TextContent.Contains(
                "Dialogs:SaveGroup:Impact:Actions:SaveAnyway",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Render_WhenEveryKnownImpactIsOnline_ShouldShowSuccessWithoutRestartAdvisory()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, _) = await RenderDialogAsync(context, CreateOnlineImpact(), changeCount: 1);

        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:AllOnlineTitle");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Online [1]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Restart [0]");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:ServiceStatus:Online");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:RestartRequiredTitle");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:Counts:Uncertain");
    }

    [Fact]
    public async Task Render_WhenOnlyConsumerIsUnknown_ShouldShowUncertaintyWithoutZeroServiceRestartMessage()
    {
        await using var context = new ConfigurationUiTestContext();
        var impact = new ConfigurationDefinitionChangeImpact
        {
            AffectedPublishers = [],
            ParametersWithoutKnownConsumers =
            [
                new ConfigurationParameterWithoutKnownConsumer
                {
                    DefinitionKey = "Sample.UnclaimedOptions",
                    DefinitionDisplayName = "Unclaimed options",
                    LogicalPath = LogicalPath.FromProperties("Endpoint"),
                    ParameterDisplayName = "Unclaimed endpoint"
                }
            ]
        };
        var (surface, _) = await RenderDialogAsync(context, impact, changeCount: 1);

        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UncertainImpactTitle");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Counts:Uncertain [1]");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:RestartRequiredTitle");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:AllOnlineTitle");
    }

    [Fact]
    public async Task Render_WhenKnownPublisherEvidenceIsUnresolved_ShouldShowUncertaintyWithoutConfirmedRestart()
    {
        await using var context = new ConfigurationUiTestContext();
        var impact = new ConfigurationDefinitionChangeImpact
        {
            AffectedPublishers =
            [
                new ConfigurationAffectedPublisher
                {
                    PublisherKey = "message-service",
                    Parameters =
                    [
                        CreateParameter(
                            "Sample.MessageOptions",
                            "Message options",
                            "Connection",
                            "Connection",
                            ConfigurationReloadBehaviorObservationKind.Unresolved,
                            ConfigurationReloadBehavior.Unknown)
                    ]
                }
            ],
            ParametersWithoutKnownConsumers = []
        };
        var (surface, _) = await RenderDialogAsync(context, impact, changeCount: 1);

        surface.Markup.Should().Contain("message-service");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:ServiceStatus:Uncertain");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UncertainImpactTitle");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:RestartRequiredTitle");
        surface.Markup.Should().NotContain("Dialogs:SaveGroup:Impact:UnknownConsumersTitle");
    }

    [Fact]
    public async Task Cancel_WhenSelected_ShouldCancelWithoutConfirming()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, dialog) = await RenderDialogAsync(context, CreateMixedImpact(), changeCount: 4);

        FindButton(surface, "Common:Actions:Cancel").Click();

        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WhenImpactIsAvailable_ShouldReturnApproval()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, dialog) = await RenderDialogAsync(context, CreateOnlineImpact(), changeCount: 1);

        FindButton(surface, "Dialogs:SaveGroup:Impact:Actions:ConfirmSave").Click();

        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().Be(true);
    }

    [Fact]
    public async Task Render_WhenAnalysisFails_ShouldExposeOnlyExplicitSaveAnywayApproval()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, dialog) = await RenderDialogAsync(
            context,
            impact: null,
            changeCount: 3,
            analysisError: "publisher metadata is unavailable");

        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UnavailableTitle");
        surface.Markup.Should().Contain("publisher metadata is unavailable");
        surface.FindAll("button")
            .Should().NotContain(button => button.TextContent.Contains(
                "Dialogs:SaveGroup:Impact:Actions:ConfirmSave",
                StringComparison.Ordinal));

        FindButton(surface, "Dialogs:SaveGroup:Impact:Actions:SaveAnyway").Click();

        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().Be(true);
    }

    private static async Task<(IRenderedComponent<MudDialogProvider> Surface, IDialogReference Dialog)>
        RenderDialogAsync(
            ConfigurationUiTestContext context,
            ConfigurationDefinitionChangeImpact? impact,
            int changeCount,
            string? analysisError = null)
    {
        var parameters = new DialogParameters<ConfigurationAffectedServiceConfirmationDialog>();
        parameters.Add(component => component.ChangeCount, changeCount);
        parameters.Add(component => component.Impact, impact);
        parameters.Add(component => component.AnalysisError, analysisError);
        var dialogService = context.Services.GetRequiredService<IDialogService>();
        var dialog = await dialogService.ShowAsync<ConfigurationAffectedServiceConfirmationDialog>(
            "Affected services",
            parameters);

        return (context.DialogProvider, dialog);
    }

    private static AngleSharp.Dom.IElement FindButton(
        IRenderedComponent<MudDialogProvider> surface,
        string text)
    {
        return surface.FindAll("button").Single(button => button.TextContent.Contains(text, StringComparison.Ordinal));
    }

    private static ConfigurationDefinitionChangeImpact CreateMixedImpact()
    {
        return new ConfigurationDefinitionChangeImpact
        {
            AffectedPublishers =
            [
                new ConfigurationAffectedPublisher
                {
                    PublisherKey = "flight-service",
                    Parameters =
                    [
                        CreateParameter(
                            "Sample.AiOptions",
                            "AI module options",
                            "Url",
                            "Git URL",
                            ConfigurationReloadBehaviorObservationKind.Inferred,
                            ConfigurationReloadBehavior.OnlineReloadable),
                        CreateParameter(
                            "Sample.AiOptions",
                            "AI module options",
                            "Token",
                            "Git token",
                            ConfigurationReloadBehaviorObservationKind.Declared,
                            ConfigurationReloadBehavior.RequiresRestart)
                    ]
                },
                new ConfigurationAffectedPublisher
                {
                    PublisherKey = "message-service",
                    Parameters =
                    [
                        CreateParameter(
                            "Sample.MessageOptions",
                            "Message options",
                            "Connection",
                            "Connection",
                            ConfigurationReloadBehaviorObservationKind.Unresolved,
                            ConfigurationReloadBehavior.Unknown)
                    ]
                }
            ],
            ParametersWithoutKnownConsumers =
            [
                new ConfigurationParameterWithoutKnownConsumer
                {
                    DefinitionKey = "Sample.UnclaimedOptions",
                    DefinitionDisplayName = "Unclaimed options",
                    LogicalPath = LogicalPath.FromProperties("Endpoint"),
                    ParameterDisplayName = "Unclaimed endpoint"
                }
            ]
        };
    }

    private static ConfigurationDefinitionChangeImpact CreateOnlineImpact()
    {
        return new ConfigurationDefinitionChangeImpact
        {
            AffectedPublishers =
            [
                new ConfigurationAffectedPublisher
                {
                    PublisherKey = "flight-service",
                    Parameters =
                    [
                        CreateParameter(
                            "Sample.AiOptions",
                            "AI module options",
                            "Url",
                            "Git URL",
                            ConfigurationReloadBehaviorObservationKind.Inferred,
                            ConfigurationReloadBehavior.OnlineReloadable)
                    ]
                }
            ],
            ParametersWithoutKnownConsumers = []
        };
    }

    private static ConfigurationAffectedParameter CreateParameter(
        string definitionKey,
        string definitionDisplayName,
        string propertyName,
        string parameterDisplayName,
        ConfigurationReloadBehaviorObservationKind observationKind,
        ConfigurationReloadBehavior reloadBehavior)
    {
        return new ConfigurationAffectedParameter
        {
            DefinitionKey = definitionKey,
            DefinitionDisplayName = definitionDisplayName,
            LogicalPath = LogicalPath.FromProperties("Git", propertyName),
            ParameterDisplayName = parameterDisplayName,
            ObservationKind = observationKind,
            ReloadBehavior = reloadBehavior
        };
    }
}
