using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Core.TypeDiscovery.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDiagnosticsFacadeTests
{
    [Fact]
    public void GetSnapshot_WhenCompositionIsTerminal_ShouldReturnOneStableRevisionAndReference()
    {
        using var host = CreateHost();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

        var first = facade.GetSnapshot();
        var second = facade.GetSnapshot();

        first.Status.Should().Be(ResStatus.Ok);
        first.Data.Should().NotBeNull();
        first.Data!.IsFinal.Should().BeTrue();
        first.Data.Outcome.Should().Be(ModuleCompositionOutcome.Succeeded);
        second.Data.Should().BeSameAs(first.Data);
        second.Data!.Revision.Should().Be(first.Data.Revision);
        first.Data.Summary.PerformanceBudgets.Should().BeEmpty();
    }

    [Fact]
    public void GetSnapshot_WhenConfiguredBudgetIsExceeded_ShouldEmitStructuredWarning()
    {
        using var host = CreateHost(configureBudgets: true);
        var snapshot = host.Services.GetRequiredService<ModuleDiagnosticsFacade>().GetSnapshot().Data!;

        snapshot.Outcome.Should().Be(ModuleCompositionOutcome.Degraded);
        snapshot.Summary.PerformanceBudgets.Should().ContainSingle(evaluation =>
            evaluation.Kind == ModulePerformanceBudgetKind.TotalComposition && evaluation.IsExceeded);
        snapshot.Findings.Should().ContainSingle(finding =>
            finding.Code == ModuleDiagnosticFindingCodes.PERFORMANCE_BUDGET_EXCEEDED
            && finding.Severity == ModuleDiagnosticFindingSeverity.Warning
            && finding.Evidence.Kind == ModuleDiagnosticFindingEvidenceKind.PerformanceBudget);
    }

    [Fact]
    public void GetModuleOptions_WhenProjectionIsConfigured_ShouldExposeOnlyBoundedSafeEntries()
    {
        using var host = CreateHost();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        var module = facade.GetSnapshot().Data!.Modules.Single(item =>
            item.TypeName == nameof(DiagnosticsConsumerModule));

        var diagnostics = facade.GetModuleOptions(module.ModuleKey);

        diagnostics.Status.Should().Be(ResStatus.Ok);
        diagnostics.Data!.IsConfigured.Should().BeTrue();
        diagnostics.Data.Entries.Should().Contain(entry =>
            entry.Name == "Mode"
            && entry.Kind == ModuleOptionDiagnosticValueKind.Value
            && entry.Value == "probe");
        diagnostics.Data.Entries.Should().Contain(entry =>
            entry.Name == "ApiKey"
            && entry.Kind == ModuleOptionDiagnosticValueKind.Presence
            && entry.IsPresent == true);
        diagnostics.Data.Entries.Should().Contain(entry =>
            entry.Name == "EndpointCount"
            && entry.Kind == ModuleOptionDiagnosticValueKind.Count
            && entry.Count == 3);
        diagnostics.Data.Entries.Should().Contain(entry =>
            entry.Name == "Unavailable"
            && entry.Kind == ModuleOptionDiagnosticValueKind.Unavailable);
        diagnostics.Data.Entries.Should().NotContain(entry => entry.Value == "super-secret");
    }

    [Fact]
    public void CreateExport_ShouldUsePortableIdsAndOmitOptionsPathsAndRawFailures()
    {
        using var host = CreateHost();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

        var export = facade.CreateExport();
        var json = JsonSerializer.Serialize(export.Data);

        export.Status.Should().Be(ResStatus.Ok);
        export.Data!.Modules.Should().Contain(module =>
            module.ModuleId == ModuleKey.FromModuleType(typeof(DiagnosticsConsumerModule)).Id);
        export.Data.Edges.Should().Contain(edge =>
            edge.SourceModuleId == ModuleKey.FromModuleType(typeof(DiagnosticsConsumerModule)).Id
            && edge.TargetModuleId == ModuleKey.FromModuleType(typeof(DiagnosticsProviderModule)).Id);
        json.Should().NotContain("super-secret");
        json.Should().NotContain("OptionTypeName");
        json.Should().NotContain("Location");
        json.Should().NotContain("StackTrace");
    }

    [Fact]
    public void GetSnapshot_WhenTerminalCollectionsArePublished_ShouldRejectMutationAndKeepCachedIdentity()
    {
        using var host = CreateHost(configureBudgets: true);
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();
        var snapshot = facade.GetSnapshot().Data!;
        IList<ModuleDiagnosticsModule> modules = snapshot.Modules;
        IDictionary<string, string> arguments = snapshot.Findings.Single().Arguments;

        Action replaceModule = () => modules[0] = modules[0];
        Action addArgument = () => arguments.Add("injected", "value");

        replaceModule.Should().Throw<NotSupportedException>();
        addArgument.Should().Throw<NotSupportedException>();
        facade.GetSnapshot().Data.Should().BeSameAs(snapshot);
        snapshot.Findings.Single().Arguments.Should().NotContainKey("injected");
    }

    [Fact]
    public void GetAssemblyInventory_WhenNoPlanRequiresEnumeration_ShouldReportResolvedNotScannedAssemblies()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(ModuleDiagnosticsFacadeTests).Assembly));
            monica.AddModuleSystem();
        });
        using var host = builder.Build();

        var inventory = host.Services.GetRequiredService<ModuleDiagnosticsFacade>()
            .GetAssemblyInventory().Data!;

        inventory.Assemblies.Should().Contain(record =>
            record.Name == typeof(ModuleDiagnosticsFacadeTests).Assembly.GetName().Name
            && record.Outcome == TypeDiscoveryAssemblyOutcome.ResolvedNotScanned);
        inventory.ScannedCount.Should().Be(0);
        inventory.NotScannedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetSnapshot_WhenWebCompositionCallbackFails_ShouldBecomeTerminalWithOneSanitizedFinding()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            monica.AddModule<FailingDiagnosticsWebModule, FailingDiagnosticsWebModuleOption>();
        });
        using var app = builder.Build();

        Action configurePipeline = () => app.UseMonica();

        configurePipeline.Should().Throw<InvalidOperationException>()
            .WithMessage("*sensitive-web-failure*");
        var snapshot = app.Services.GetRequiredService<ModuleDiagnosticsFacade>().GetSnapshot().Data!;
        snapshot.IsFinal.Should().BeTrue();
        snapshot.Outcome.Should().Be(ModuleCompositionOutcome.Failed);
        snapshot.Findings.Should().ContainSingle(finding =>
            finding.Code == ModuleDiagnosticFindingCodes.COMPOSITION_FAILED
            && finding.Evidence.Kind == ModuleDiagnosticFindingEvidenceKind.CompositionFailure);
        JsonSerializer.Serialize(snapshot.Findings).Should().NotContain("sensitive-web-failure");
    }

    [Fact]
    public void GetAssemblyInventory_WhenReadRepeatedly_ShouldReturnTheCachedInventory()
    {
        using var host = CreateHost();
        var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

        var first = facade.GetAssemblyInventory();
        var second = facade.GetAssemblyInventory();

        first.Status.Should().Be(ResStatus.Ok);
        second.Data.Should().BeSameAs(first.Data);
        first.Data!.Assemblies.Should().OnlyContain(assembly =>
            Enum.IsDefined(assembly.Outcome));
    }

    [Fact]
    public void GetSnapshot_ShouldExposeDirectEdgesAndLongestDependencyDepth()
    {
        using var host = CreateHost();
        var snapshot = host.Services.GetRequiredService<ModuleDiagnosticsFacade>().GetSnapshot().Data!;
        var consumer = snapshot.Modules.Single(module =>
            module.TypeName == nameof(DiagnosticsConsumerModule));

        snapshot.Topology.Edges.Should().ContainSingle(edge =>
            edge.SourceModule == consumer.ModuleKey
            && edge.TargetModule == ModuleKey.FromModuleType(typeof(DiagnosticsProviderModule)));
        consumer.DependencyDepth.Should().Be(1);
        snapshot.Topology.MaximumDepth.Should().BeGreaterThanOrEqualTo(1);
    }

    private static IHost CreateHost(bool configureBudgets = false)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            if (configureBudgets)
            {
                monica.ConfigureModuleSystem(options => options.StartupPerformanceBudgets = new()
                {
                    TotalComposition = TimeSpan.FromTicks(1)
                });
            }

            monica.AddModuleSystem(options => options
                .ExposeModuleOptions<DiagnosticsConsumerModule, DiagnosticsConsumerModuleOption>(diagnostics =>
                    diagnostics
                        .ExposeValue("Mode", static option => option.Mode)
                        .ExposePresence("ApiKey", static option => !string.IsNullOrWhiteSpace(option.ApiKey))
                        .ExposeCount("EndpointCount", static option => option.EndpointCount)
                        .ExposeValue<string>("Unavailable", static _ => throw new InvalidOperationException())));
            monica.AddModule<DiagnosticsConsumerModule, DiagnosticsConsumerModuleOption>(options =>
            {
                options.Mode = "probe";
                options.ApiKey = "super-secret";
                options.EndpointCount = 3;
            });
        });
        return builder.Build();
    }
}

internal sealed class DiagnosticsConsumerModule : MonicaModule<DiagnosticsConsumerModuleOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<DiagnosticsProviderModule, DiagnosticsProviderModuleOption>();
    }
}

internal sealed class DiagnosticsConsumerModuleOption : ModuleOptions<DiagnosticsConsumerModule>
{
    public string Mode { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int EndpointCount { get; set; }
}

internal sealed class DiagnosticsProviderModule : MonicaModule<DiagnosticsProviderModuleOption>;

internal sealed class DiagnosticsProviderModuleOption : ModuleOptions<DiagnosticsProviderModule>;

internal sealed class FailingDiagnosticsWebModule
    : MonicaModule<FailingDiagnosticsWebModuleOption>, IWebModule
{
    public override void ConfigureApplicationBuilder(WebModuleContext<FailingDiagnosticsWebModuleOption> context)
    {
        throw new InvalidOperationException("sensitive-web-failure");
    }
}

internal sealed class FailingDiagnosticsWebModuleOption : ModuleOptions<FailingDiagnosticsWebModule>;
