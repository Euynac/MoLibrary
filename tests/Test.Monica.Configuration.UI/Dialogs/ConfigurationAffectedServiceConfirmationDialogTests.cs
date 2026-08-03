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
    public async Task Render_WhenImpactIsAvailable_ShouldShowPublishersDefinitionsAndUnknownConsumers()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, _) = await RenderDialogAsync(context, CreateImpact());

        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:Summary [3, 1]");
        surface.Markup.Should().Contain("flight-service");
        surface.Markup.Should().Contain("AI module options");
        surface.Markup.Should().Contain("Sample.AiOptions");
        surface.Markup.Should().Contain("Dialogs:SaveGroup:Impact:UnknownConsumersTitle [1]");
        surface.Markup.Should().Contain("Unclaimed options");
        surface.Markup.Should().Contain("Sample.UnclaimedOptions");
        FindButton(surface, "Dialogs:SaveGroup:Impact:Actions:ConfirmSave").Should().NotBeNull();
        surface.FindAll("button")
            .Should().NotContain(button => button.TextContent.Contains(
                "Dialogs:SaveGroup:Impact:Actions:SaveAnyway",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancel_WhenSelected_ShouldCancelWithoutConfirming()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, dialog) = await RenderDialogAsync(context, CreateImpact());

        FindButton(surface, "Common:Actions:Cancel").Click();

        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WhenImpactIsAvailable_ShouldReturnApproval()
    {
        await using var context = new ConfigurationUiTestContext();
        var (surface, dialog) = await RenderDialogAsync(context, CreateImpact());

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
            string? analysisError = null)
    {
        var parameters = new DialogParameters<ConfigurationAffectedServiceConfirmationDialog>();
        parameters.Add(component => component.ChangeCount, 3);
        parameters.Add(component => component.Impact, impact);
        parameters.Add(component => component.AnalysisError, analysisError);
        parameters.Add(component => component.DefinitionDisplayNames, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sample.AiOptions"] = "AI module options",
            ["Sample.UnclaimedOptions"] = "Unclaimed options"
        });
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

    private static ConfigurationDefinitionChangeImpact CreateImpact()
    {
        return new ConfigurationDefinitionChangeImpact
        {
            DefinitionKeys = ["Sample.AiOptions", "Sample.UnclaimedOptions"],
            AffectedPublishers =
            [
                new ConfigurationAffectedPublisher
                {
                    PublisherKey = "flight-service",
                    DefinitionKeys = ["Sample.AiOptions"]
                }
            ],
            DefinitionsWithoutKnownConsumers = ["Sample.UnclaimedOptions"]
        };
    }
}
