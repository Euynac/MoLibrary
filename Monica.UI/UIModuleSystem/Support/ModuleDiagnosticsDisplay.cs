using Microsoft.Extensions.Localization;
using System.Globalization;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.UI.Localization;

namespace Monica.UI.UIModuleSystem.Support;

internal static class ModuleDiagnosticsDisplay
{
    internal static string Duration(double milliseconds) => milliseconds switch
    {
        < 0.01 => "<0.01 ms",
        < 10 => string.Create(CultureInfo.InvariantCulture, $"{milliseconds:F2} ms"),
        < 1_000 => string.Create(CultureInfo.InvariantCulture, $"{milliseconds:F1} ms"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 1_000:F2} s")
    };

    internal static string Outcome(
        ModuleCompositionOutcome? outcome,
        IStringLocalizer<ModuleSystemResource> localizer) => outcome switch
    {
        ModuleCompositionOutcome.Succeeded => localizer["Enums:Outcome:Succeeded"],
        ModuleCompositionOutcome.Degraded => localizer["Enums:Outcome:Degraded"],
        ModuleCompositionOutcome.Failed => localizer["Enums:Outcome:Failed"],
        _ => localizer["Enums:Outcome:Live"]
    };

    internal static string Phase(ModulePhase phase, IStringLocalizer<ModuleSystemResource> localizer) =>
        localizer[$"Enums:Phase:{phase}"];

    internal static string Stage(ModuleSystemStage stage, IStringLocalizer<ModuleSystemResource> localizer) =>
        localizer[$"Enums:Stage:{stage}"];

    internal static string Callback(ModuleCallbackKind kind, IStringLocalizer<ModuleSystemResource> localizer) =>
        localizer[$"Enums:Callback:{kind}"];

    internal static ModuleDiagnosticsTraceDisplay TraceSpan(
        ModuleDiagnosticsTraceSpan span,
        ModuleDiagnosticsSnapshot snapshot,
        IStringLocalizer<ModuleSystemResource> localizer)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(localizer);

        var moduleName = span.ModuleKey is { } moduleKey
            ? snapshot.Modules.FirstOrDefault(module => module.ModuleKey == moduleKey)?.TypeName
            : null;
        var primary = span.SystemStage is { } stage
            ? Stage(stage, localizer)
            : !string.IsNullOrWhiteSpace(moduleName)
                ? moduleName
                : span.Kind == ModuleDiagnosticsTraceSpanKind.StartupBarrier && span.Barrier is { } barrier
                    ? localizer[$"Enums:Barrier:{barrier}"]
                    : span.Name ?? localizer[$"Enums:TraceKind:{span.Kind}"];

        var activity = span.CallbackKind is { } callback
            ? Callback(callback, localizer)
            : span.Kind == ModuleDiagnosticsTraceSpanKind.StartupBarrier && span.Barrier is { } activityBarrier
                ? localizer[$"Enums:Barrier:{activityBarrier}"]
                : span.WorkItemId ?? span.Name ?? localizer[$"Enums:TraceKind:{span.Kind}"];
        var secondary = span.ModulePhase is { } phase
            ? $"{activity} · {Phase(phase, localizer)}"
            : activity;

        return new ModuleDiagnosticsTraceDisplay(primary, secondary);
    }

    internal static string Finding(
        ModuleDiagnosticFinding finding,
        IStringLocalizer<ModuleSystemResource> localizer)
    {
        return finding.Code switch
        {
            ModuleDiagnosticFindingCodes.PERFORMANCE_BUDGET_EXCEEDED => localizer[
                "Findings:PerformanceBudgetExceeded",
                finding.Evidence.PerformanceMetric is { } metric
                    ? localizer[$"Enums:Budget:{metric}"]
                    : localizer["Common:Unknown"],
                Duration(finding.Evidence.ActualDurationMs ?? 0),
                Duration(finding.Evidence.LimitDurationMs ?? 0)],
            ModuleDiagnosticFindingCodes.REGISTRATION_FAILED => localizer[
                "Findings:RegistrationFailed",
                finding.RelatedModules.FirstOrDefault().Value,
                finding.Evidence.ModulePhase is { } phase
                    ? Phase(phase, localizer)
                    : localizer["Common:Unknown"]],
            ModuleDiagnosticFindingCodes.STARTUP_WORK_FAILED => localizer[
                "Findings:StartupWorkFailed",
                finding.RelatedModules.FirstOrDefault().Value,
                finding.Evidence.WorkItemId ?? string.Empty],
            ModuleDiagnosticFindingCodes.STARTUP_WORK_COMMIT_FAILED => localizer[
                "Findings:StartupWorkCommitFailed",
                finding.RelatedModules.FirstOrDefault().Value,
                finding.Evidence.ModulePhase is { } phase
                    ? Phase(phase, localizer)
                    : localizer["Common:Unknown"]],
            ModuleDiagnosticFindingCodes.COMPOSITION_FAILED => localizer["Findings:CompositionFailed"],
            _ => localizer["Findings:Unknown", finding.Code]
        };
    }
}

internal sealed record ModuleDiagnosticsTraceDisplay(string Primary, string Secondary);
