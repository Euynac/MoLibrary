using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDiagnosticsSnapshotLifecycleTests
{
    private static readonly TimeSpan HANG_GUARD = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GetSnapshot_WhenNoBarrierWorkTransitions_ShouldAdvanceUntilOneFinalReferenceRemains()
    {
        using var gate = new ControlledWorkGate();
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureModuleSystem(static options => options.MaxConcurrentStartupWorkItems = 2);
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddModuleSystem();
            monica.AddModule<StartupWorkProbeModuleOne, StartupWorkProbeModuleOneOption>(options =>
                options.AddConfigureServicesWork(
                    "live-diagnostics-work",
                    gate.Run,
                    ModuleStartupWorkBarrier.NoBarrier));
        });
        IHost? host = null;

        try
        {
            await gate.Entered.Task.WaitAsync(HANG_GUARD, TestContext.Current.CancellationToken);
            host = builder.Build();
            var application = host.Services.GetRequiredService<global::Monica.Core.MonicaApplication>();
            var facade = host.Services.GetRequiredService<ModuleDiagnosticsFacade>();

            var firstResult = facade.GetSnapshot();
            var secondResult = facade.GetSnapshot();

            firstResult.Status.Should().Be(ResStatus.Ok);
            secondResult.Status.Should().Be(ResStatus.Ok);
            firstResult.Data.Should().NotBeNull();
            secondResult.Data.Should().NotBeNull();
            var first = firstResult.Data!;
            var second = secondResult.Data!;
            first.IsFinal.Should().BeFalse();
            second.IsFinal.Should().BeFalse();
            second.Revision.Should().BeGreaterThan(first.Revision);
            second.Should().NotBeSameAs(first);
            second.TraceSpans.Should().ContainSingle(span =>
                span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork
                && span.Name == "live-diagnostics-work"
                && !span.IsComplete);

            gate.Release();
            application.Modules.DrainStartupWork();

            var terminalResult = facade.GetSnapshot();
            var repeatedResult = facade.GetSnapshot();

            terminalResult.Status.Should().Be(ResStatus.Ok);
            repeatedResult.Status.Should().Be(ResStatus.Ok);
            terminalResult.Data.Should().NotBeNull();
            var terminal = terminalResult.Data!;
            terminal.IsFinal.Should().BeTrue();
            terminal.Outcome.Should().Be(ModuleCompositionOutcome.Succeeded);
            terminal.Revision.Should().BeGreaterThan(second.Revision);
            repeatedResult.Data.Should().BeSameAs(terminal);
            repeatedResult.Data!.Revision.Should().Be(terminal.Revision);
            terminal.TraceSpans.Should().ContainSingle(span =>
                span.Kind == ModuleDiagnosticsTraceSpanKind.StartupWork
                && span.Name == "live-diagnostics-work"
                && span.IsComplete);
        }
        finally
        {
            gate.Release();
            host?.Dispose();
        }
    }

    private sealed class ControlledWorkGate : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(initialState: false);

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Run()
        {
            Entered.TrySetResult();
            if (!_release.Wait(HANG_GUARD))
            {
                throw new TimeoutException("The controlled diagnostics work was not released.");
            }
        }

        internal void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }
}
