using System.Collections.Immutable;
using System.Globalization;
using AwesomeAssertions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Modules;
using Monica.UI.Localization;
using Monica.UI.Pages;
using Monica.UI.UIModuleSystem.Components;
using Monica.UI.UIModuleSystem.State;
using Monica.UI.UIModuleSystem.Support;
using MudBlazor;
using Xunit;

namespace Test.Monica.UI.UIModuleSystem;

public sealed class ModuleSystemWorkbenchComponentTests
{
    [Fact]
    public async Task Unauthorized_page_never_invokes_the_diagnostics_boundary()
    {
        var facadeCalls = 0;
        await using var context = new ModuleSystemWorkbenchUiTestContext(configureServices: services =>
        {
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production));
            services.AddSingleton<IOptions<ModuleSystemUIOption>>(Options.Create(new ModuleSystemUIOption()));
            services.AddSingleton<ModuleSystemWorkbenchAccess>();
            services.AddSingleton(new ModuleSystemWorkbenchSessionFactory(
                ModuleSystemWorkbenchTestData.Calls(snapshot: () =>
                {
                    facadeCalls++;
                    return Res.Ok(ModuleSystemWorkbenchTestData.Snapshot());
                })));
        });

        var cut = context.Render<ModuleSystemPage>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Access:DeniedTitle"));
        facadeCalls.Should().Be(0);
    }

    [Fact]
    public async Task Module_query_selects_without_reopening_an_explicitly_closed_drawer()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext(configureServices: services =>
        {
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));
            services.AddSingleton<IOptions<ModuleSystemUIOption>>(Options.Create(new ModuleSystemUIOption()));
            services.AddSingleton<ModuleSystemWorkbenchAccess>();
            services.AddSingleton(new ModuleSystemWorkbenchSessionFactory(
                ModuleSystemWorkbenchTestData.Calls()));
        });

        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(
            $"/module-system-dashboard/modules?module={Uri.EscapeDataString(ModuleSystemWorkbenchTestData.BetaKey.Id)}");
        var cut = context.Render<ModuleSystemPage>(parameters => parameters
            .Add(component => component.Section, "modules"));
        cut.WaitForAssertion(() => cut.FindComponent<ModuleWorkbenchModuleDrawer>()
            .Instance.Session.IsModuleDrawerOpen.Should().BeTrue());
        var session = cut.FindComponent<ModuleWorkbenchModuleDrawer>().Instance.Session;
        session.CloseModuleDrawer();

        navigation.NavigateTo(
            $"/module-system-dashboard/performance?module={Uri.EscapeDataString(ModuleSystemWorkbenchTestData.BetaKey.Id)}");
        cut.Render(parameters => parameters
            .Add(component => component.Section, "performance"));

        session.IsModuleDrawerOpen.Should().BeFalse();
        session.SelectModule(session.SelectedModule);

        navigation.NavigateTo("/module-system-dashboard/modules");
        cut.Render(parameters => parameters
            .Add(component => component.Section, "modules"));

        session.IsModuleDrawerOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Automatically_selected_blocking_contributor_is_reflected_in_the_url_without_opening_the_drawer()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext(configureServices: services =>
        {
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));
            services.AddSingleton<IOptions<ModuleSystemUIOption>>(Options.Create(new ModuleSystemUIOption()));
            services.AddSingleton<ModuleSystemWorkbenchAccess>();
            services.AddSingleton(new ModuleSystemWorkbenchSessionFactory(
                ModuleSystemWorkbenchTestData.Calls()));
        });

        var cut = context.Render<ModuleSystemPage>();

        cut.WaitForAssertion(() =>
        {
            var drawer = cut.FindComponent<ModuleWorkbenchModuleDrawer>();
            drawer.Instance.Session.SelectedModule.Should().NotBeNull();
            drawer.Instance.Session.IsModuleDrawerOpen.Should().BeFalse();
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().Contain(
                Uri.EscapeDataString(drawer.Instance.Session.SelectedModule!.ModuleKey.Id));
        });
    }

    [Fact]
    public async Task Initial_and_failed_states_have_distinct_observable_ui()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var initial = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        var loading = context.Render<ModuleWorkbenchLoadState>(parameters => parameters
            .Add(component => component.Session, initial));
        loading.Markup.Should().Contain("Load:Loading");

        await using var failed = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        var request = failed.BeginSnapshotRequest(isInitial: true);
        failed.CompleteSnapshotRequest(request, Res.Fail("offline"));
        var error = context.Render<ModuleWorkbenchLoadState>(parameters => parameters
            .Add(component => component.Session, failed));

        error.Markup.Should().Contain("Load:FailedTitle");
        error.Markup.Should().Contain("Actions:Retry");
    }

    [Fact]
    public async Task Refresh_failure_remains_visible_above_every_workbench_section()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        var request = session.BeginSnapshotRequest(isInitial: false);
        session.CompleteSnapshotRequest(request, Res.Fail("offline"));

        var cut = context.Render<ModuleWorkbenchHeader>(parameters => parameters
            .Add(component => component.Session, session));

        cut.Markup.Should().Contain("Load:RefreshFailed");
        cut.Markup.Should().Contain("Actions:Retry");
    }

    [Fact]
    public async Task Module_catalog_renders_the_session_filter_projection()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        session.SetModuleFilters(
            "Beta",
            ModuleCatalogStateFilter.Active,
            ModuleCatalogCapabilityFilter.Web,
            assembly: null,
            onlyWithDependencies: false,
            minimumCostMs: 0);

        var cut = context.Render<ModuleWorkbenchModules>(parameters => parameters
            .Add(component => component.Session, session));

        cut.Markup.Should().Contain("BetaModule");
        cut.Markup.Should().NotContain("AlphaModule");
    }

    [Fact]
    public async Task Module_catalog_cost_meter_preserves_total_and_work_kind_proportions()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();

        var cut = context.Render<ModuleWorkbenchModules>(parameters => parameters
            .Add(component => component.Session, session));

        var betaMeter = cut.Find("[data-module-cost='BetaModule'] [role='meter']");
        ParseSvgNumber(betaMeter.GetAttribute("data-total-width")).Should().BeApproximately(1000, .001);
        ParseSvgNumber(betaMeter.GetAttribute("data-callback-width")).Should().BeApproximately(142.857, .001);
        ParseSvgNumber(betaMeter.GetAttribute("data-startup-width")).Should().BeApproximately(857.143, .001);

        var alphaMeter = cut.Find("[data-module-cost='AlphaModule'] [role='meter']");
        ParseSvgNumber(alphaMeter.GetAttribute("data-total-width")).Should().BeApproximately(428.571, .001);
    }

    [Fact]
    public async Task Module_catalog_renders_truncated_identifiers_without_clipboard_actions()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();

        var cut = context.Render<ModuleWorkbenchModules>(parameters => parameters
            .Add(component => component.Session, session));

        cut.FindAll(".identifier-value").Should().HaveCount(session.PagedModules.Count);
        cut.FindAll(".copyable-identifier").Should().BeEmpty();
        cut.FindAll("button[aria-label^='Aria:Copy']").Should().BeEmpty();
    }

    [Fact]
    public async Task Discovery_loads_the_assembly_explorer_and_defaults_to_attention_when_issues_exist()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        var inventoryCalls = 0;
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls(
            inventory: () =>
            {
                inventoryCalls++;
                return Res.Ok(ModuleSystemWorkbenchTestData.Inventory());
            }));
        await session.InitializeAsync();

        var cut = context.Render<ModuleWorkbenchDiscovery>(parameters => parameters
            .Add(component => component.Session, session));
        cut.WaitForAssertion(() =>
        {
            session.AssemblyInventoryState.Should().Be(ModuleSystemLazyLoadState.Ready);
            session.AssemblyFacet.Should().Be(AssemblyInventoryFacet.Attention);
            cut.Markup.Should().Contain("Failed.Assembly");
            cut.Markup.Should().Contain("Scanned.Assembly");
            cut.Markup.Should().Contain("Test.*");
        });

        inventoryCalls.Should().Be(1);
        session.FilteredAssemblies.Should().ContainSingle(record =>
            record.Outcome == TypeDiscoveryAssemblyOutcome.ResolutionFailed);
    }

    [Fact]
    public async Task Selected_module_drawer_renders_the_synchronized_selection()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        session.SelectModule(session.Snapshot!.Modules[1]);

        var cut = context.Render<ModuleWorkbenchModuleDrawer>(parameters => parameters
            .Add(component => component.Session, session));

        cut.Markup.Should().Contain("BetaModule");
        cut.Markup.Should().Contain("ModuleDrawer:Tabs:Summary");
        session.IsModuleDrawerOpen.Should().BeTrue();
    }

    [Theory]
    [InlineData("en-US", "Type discovery took 12.0 ms, exceeding the configured 8.00 ms budget.")]
    [InlineData("zh-CN", "类型发现 实际耗时 12.0 ms，超过配置的 8.00 ms 预算。")]
    public async Task Structured_finding_code_is_rendered_from_the_real_bilingual_resources(
        string cultureName,
        string expectedMessage)
    {
        var finding = new ModuleDiagnosticFinding
        {
            Code = ModuleDiagnosticFindingCodes.PERFORMANCE_BUDGET_EXCEEDED,
            Severity = ModuleDiagnosticFindingSeverity.Warning,
            Evidence = new ModuleDiagnosticFindingEvidence
            {
                Kind = ModuleDiagnosticFindingEvidenceKind.PerformanceBudget,
                PerformanceMetric = ModulePerformanceBudgetKind.TypeDiscovery,
                ActualDurationMs = 12,
                LimitDurationMs = 8
            }
        };
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddLocalization().AddResource<ModuleSystemResource>();
        });
        using var localizationHost = builder.Build();
        var localizer = localizationHost.Services
            .GetRequiredService<IStringLocalizer<ModuleSystemResource>>();
        using var culture = new CultureScope(cultureName);
        await using var context = new ModuleSystemWorkbenchUiTestContext(localizer);
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls(
            snapshot: () => Res.Ok(ModuleSystemWorkbenchTestData.Snapshot(findings: [finding]))));
        await session.InitializeAsync();

        var cut = context.Render<ModuleWorkbenchOverview>(parameters => parameters
            .Add(component => component.Session, session));

        cut.Markup.Should().Contain(expectedMessage);
        cut.Markup.Should().NotContain("Findings:PerformanceBudgetExceeded");
        cut.Markup.Should().Contain(ModuleDiagnosticFindingCodes.PERFORMANCE_BUDGET_EXCEEDED);
    }

    [Fact]
    public async Task CriticalPath_WhenChainExceedsTenSpans_ShouldRenderCompleteOrderAndHighlightSelection()
    {
        var workSpans = Enumerable.Range(0, 12)
            .Select(index => new ModuleDiagnosticsTraceSpan
            {
                SpanId = $"work-span-{index:D2}",
                Kind = ModuleDiagnosticsTraceSpanKind.StartupWork,
                ModuleKey = ModuleSystemWorkbenchTestData.BetaKey,
                WorkItemId = $"work-{index:D2}",
                Name = $"work-{index:D2}",
                StartedOffsetMs = index,
                EndedOffsetMs = index + 1
            })
            .ToArray();
        var barrierSpan = new ModuleDiagnosticsTraceSpan
        {
            SpanId = "barrier-span",
            Kind = ModuleDiagnosticsTraceSpanKind.StartupBarrier,
            Barrier = ModuleStartupWorkBarrier.BeforePostConfigureServices,
            StartedOffsetMs = 12,
            EndedOffsetMs = 13
        };
        var unrelatedSpan = new ModuleDiagnosticsTraceSpan
        {
            SpanId = "unrelated-span",
            Kind = ModuleDiagnosticsTraceSpanKind.SystemStage,
            StartedOffsetMs = 13,
            EndedOffsetMs = 14
        };
        var blockingChain = workSpans.Select((span, index) => new ModuleBlockingChainSegment
        {
            SegmentId = $"segment-{index:D2}",
            BarrierSpanId = barrierSpan.SpanId,
            WorkItemId = span.WorkItemId!,
            WorkSpanId = span.SpanId,
            ModuleKey = ModuleSystemWorkbenchTestData.BetaKey,
            Barrier = ModuleStartupWorkBarrier.BeforePostConfigureServices,
            BlockingDurationMs = 12 - index,
            IsBarrierReleaser = index == workSpans.Length - 1
        }).ToImmutableArray();
        var snapshot = ModuleSystemWorkbenchTestData.Snapshot() with
        {
            TraceSpans = [.. workSpans, barrierSpan, unrelatedSpan],
            BlockingChain = blockingChain
        };
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(
            ModuleSystemWorkbenchTestData.Calls(snapshot: () => Res.Ok(snapshot)));
        await session.InitializeAsync();

        var ribbon = context.Render<ModuleWorkbenchCriticalPathRibbon>(parameters => parameters
            .Add(component => component.Session, session));
        var buttons = ribbon.FindAll("button.ribbon-segment");

        buttons.Select(button => button.GetAttribute("data-span-id"))
            .Should().Equal(workSpans.Select(static span => span.SpanId).Append(barrierSpan.SpanId));
        buttons.Should().HaveCount(13);
        ribbon.Markup.Should().NotContain(unrelatedSpan.SpanId);

        buttons[11].Click();
        session.SelectedTraceSpanId.Should().Be(workSpans[11].SpanId);

        var performance = context.Render<ModuleWorkbenchPerformance>(parameters => parameters
            .Add(component => component.Session, session));
        performance.Find($"rect.timeline-bar-selected[data-span-id='{workSpans[11].SpanId}']")
            .Should().NotBeNull();
        performance.Find($".timeline-value[data-span-id='{workSpans[11].SpanId}']")
            .GetAttribute("aria-current").Should().Be("true");
        performance.FindAll("tr.timeline-row-selected").Should().ContainSingle();
    }

    [Fact]
    public async Task Waterfall_interval_can_drive_selection_and_focus_without_losing_exact_timing()
    {
        await using var context = new ModuleSystemWorkbenchUiTestContext();
        await using var session = new ModuleSystemWorkbenchSession(ModuleSystemWorkbenchTestData.Calls());
        await session.InitializeAsync();
        var cut = context.Render<ModuleCompositionWaterfall>(parameters => parameters
            .Add(component => component.Session, session));

        cut.Find("button.timeline-waterfall__track-button[data-span-id='span-alpha']").Click();

        session.SelectedTraceSpanId.Should().Be("span-alpha");
        session.SelectedModule!.TypeName.Should().Be("AlphaModule");
        cut.Find(".timeline-waterfall__selection").TextContent.Should().Contain("AlphaModule");

        var waterfall = cut.Find("section.timeline-waterfall");
        ParseSvgNumber(waterfall.GetAttribute("data-axis-width")).Should().BeApproximately(24, .001);
        cut.Find("button[aria-label='Performance:Waterfall:FocusSelected']").Click();

        cut.Find("section.timeline-waterfall").GetAttribute("data-zoom-level").Should().Be("2");
        ParseSvgNumber(cut.Find("section.timeline-waterfall").GetAttribute("data-axis-width"))
            .Should().BeApproximately(12, .001);
        cut.Find("rect.timeline-bar-selected[data-span-id='span-alpha']").Should().NotBeNull();
    }

    private static double ParseSvgNumber(string? value) =>
        double.Parse(value!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        internal CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
