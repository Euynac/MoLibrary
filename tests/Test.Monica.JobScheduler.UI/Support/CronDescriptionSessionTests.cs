using AwesomeAssertions;
using Microsoft.JSInterop;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Support;

public sealed class CronDescriptionSessionTests
{
    private const string EXPECTED_MODULE_PATH =
        "./_content/Monica.JobScheduler.UI/js/cron-descriptions.js";

    [Fact]
    public async Task DescribeAsync_WhenCallsOverlap_ShouldImportModuleOnce()
    {
        var module = new RecordingCronModule([]);
        var runtime = new ImmediateJsRuntime(module);
        await using var session = new CronDescriptionSession(runtime);
        IReadOnlyList<CronDescriptionRequest> requests =
        [
            new("cleanup", "0 */5 * * * *")
        ];

        await Task.WhenAll(
            session.DescribeAsync(requests, "en-US", Xunit.TestContext.Current.CancellationToken).AsTask(),
            session.DescribeAsync(requests, "en-US", Xunit.TestContext.Current.CancellationToken).AsTask(),
            session.DescribeAsync(requests, "en-US", Xunit.TestContext.Current.CancellationToken).AsTask());

        runtime.Imports.Should().ContainSingle();
        module.Invocations.Should().HaveCount(3);
    }

    [Fact]
    public async Task DescribeAsync_ShouldSendOneBatchWithCultureThroughTheOwnedModule()
    {
        IReadOnlyList<CronDescriptionRequest> requests =
        [
            new("cleanup", "0 */5 * * * *"),
            new("nightly", "0 0 1 * * *")
        ];
        CronDescriptionResult[] expected =
        [
            new("cleanup", "Every 5 minutes", null),
            new("nightly", "At 01:00", null)
        ];
        var module = new RecordingCronModule(expected);
        var runtime = new ImmediateJsRuntime(module);
        await using var session = new CronDescriptionSession(runtime);

        var actual = await session.DescribeAsync(requests, "zh-CN", Xunit.TestContext.Current.CancellationToken);

        var import = runtime.Imports.Should().ContainSingle().Which;
        import.Identifier.Should().Be("import");
        import.Arguments.Should().ContainSingle().Which.Should().Be(EXPECTED_MODULE_PATH);

        var invocation = module.Invocations.Should().ContainSingle().Which;
        invocation.Identifier.Should().Be("describeCronExpressions");
        invocation.Arguments.Should().HaveCount(2);
        invocation.Arguments[0].Should().BeSameAs(requests);
        invocation.Arguments[1].Should().Be("zh-CN");
        actual.Should().Equal(expected);
    }

    [Fact]
    public async Task DisposeAsync_WhenImportCompletesLate_ShouldObserveAndDisposeTheLateModule()
    {
        var module = new RecordingCronModule([]);
        var runtime = new DelayedJsRuntime();
        var session = new CronDescriptionSession(runtime);
        IReadOnlyList<CronDescriptionRequest> requests =
        [
            new("cleanup", "0 */5 * * * *")
        ];

        var description = session.DescribeAsync(
            requests,
            "en-US",
            Xunit.TestContext.Current.CancellationToken).AsTask();
        await runtime.ImportStarted;
        var disposal = session.DisposeAsync().AsTask();

        runtime.CompleteImport(module);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => description);
        await disposal;
        runtime.ModulePath.Should().Be(EXPECTED_MODULE_PATH);
        module.DisposeCount.Should().Be(1);
        module.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_WhenCallerCancelsPendingImport_ShouldObserveAndDisposeTheLateModule()
    {
        var module = new RecordingCronModule([]);
        var runtime = new DelayedJsRuntime();
        var session = new CronDescriptionSession(runtime);
        using var cancellation = new CancellationTokenSource();
        IReadOnlyList<CronDescriptionRequest> requests =
        [
            new("cleanup", "0 */5 * * * *")
        ];

        var description = session.DescribeAsync(requests, "en-US", cancellation.Token).AsTask();
        await runtime.ImportStarted;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => description);
        var disposal = session.DisposeAsync().AsTask();
        runtime.CompleteImport(module);

        await disposal;
        await session.DisposeAsync();
        module.DisposeCount.Should().Be(1);
        module.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenAbandonedImportIsCancelled_ShouldCompleteCleanly()
    {
        var runtime = new DelayedJsRuntime();
        var session = new CronDescriptionSession(runtime);
        using var cancellation = new CancellationTokenSource();
        IReadOnlyList<CronDescriptionRequest> requests =
        [
            new("cleanup", "0 */5 * * * *")
        ];

        var description = session.DescribeAsync(requests, "en-US", cancellation.Token).AsTask();
        await runtime.ImportStarted;
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => description);

        var disposal = session.DisposeAsync().AsTask();
        runtime.CancelImport();

        await disposal;
        await session.DisposeAsync();
    }

    private sealed record JsInvocation(string Identifier, object?[] Arguments);

    private sealed class ImmediateJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public List<JsInvocation> Imports { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Imports.Add(new JsInvocation(identifier, args?.ToArray() ?? []));
            return ValueTask.FromResult((TValue)module);
        }
    }

    private sealed class DelayedJsRuntime : IJSRuntime
    {
        private readonly TaskCompletionSource _importStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IJSObjectReference> _module =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ImportStarted => _importStarted.Task;

        public string? ModulePath { get; private set; }

        public void CompleteImport(IJSObjectReference module) => _module.SetResult(module);

        public void CancelImport() => _module.SetCanceled();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            identifier.Should().Be("import");
            ModulePath = args.Should().ContainSingle().Which.Should().BeOfType<string>().Which;
            _importStarted.SetResult();
            return new ValueTask<TValue>(CompleteAsync<TValue>());
        }

        private async Task<TValue> CompleteAsync<TValue>() => (TValue)await _module.Task;
    }

    private sealed class RecordingCronModule(CronDescriptionResult[] results) : IJSObjectReference
    {
        public List<JsInvocation> Invocations { get; } = [];

        public int DisposeCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Invocations.Add(new JsInvocation(identifier, args?.ToArray() ?? []));
            return ValueTask.FromResult((TValue)(object)results);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
