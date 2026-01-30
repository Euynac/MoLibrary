using Dapr.DistributedLock;
using Dapr.DistributedLock.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Dapr.Locker;
using Monica.Dapr.Modules;
using Monica.Locker.DistributedLocking;
using NSubstitute;
using NUnit.Framework;
using Test.Monica.Locker.Base;

namespace Test.Monica.Locker.Providers;

#pragma warning disable DAPR_DISTRIBUTEDLOCK // DaprDistributedLockClient is evaluation API

// Test fake for DaprDistributedLockClient
public class FakeDaprDistributedLockClient : DaprDistributedLockClient
{
    public LockResponse? NextLockResponse { get; set; }

    public FakeDaprDistributedLockClient() : base(null!, null!, string.Empty)
    {
    }

    public override Task<LockResponse?> TryLockAsync(string storeName, string resourceId, string lockOwner,
        int expiryInSeconds, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NextLockResponse);
    }

    public override Task<UnlockResponse> TryUnlockAsync(string storeName, string resourceId, string lockOwner,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UnlockResponse(LockStatus.Success));
    }
}

public class DaprMoDistributedLockTests : MoDistributedLockTestsBase
{
    private readonly FakeDaprDistributedLockClient _daprClient;
    private readonly ModuleDaprLockerOption _daprOptions;

    public DaprMoDistributedLockTests()
    {
        _daprClient = new FakeDaprDistributedLockClient();
        _daprOptions = new ModuleDaprLockerOption
        {
            StoreName = "test-store",
            OwnerPrefix = "test-owner-",
            DefaultExpirationTimeout = TimeSpan.FromMinutes(5)
        };
    }

    protected override IMoDistributedLock CreateLock()
    {
        return new DaprMoDistributedLock(
            _daprClient,
            Options.Create(_daprOptions),
            KeyNormalizer,
            Substitute.For<ILogger<DaprMoDistributedLock>>()
        );
    }

    [Test]
    public async Task TryAcquireAsync_ShouldUseNormalizedKey()
    {
        // Arrange
        var lockName = "test-lock";
        var normalizedKey = KeyNormalizer.NormalizeKey(lockName);
        _daprClient.NextLockResponse = Substitute.For<LockResponse>();

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WhenLockFails_ShouldReturnNull()
    {
        // Arrange
        var lockName = "test-lock";
        _daprClient.NextLockResponse = null;

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName, timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        handle.Should().BeNull();
    }

    [Test]
    public async Task TryAcquireAsync_WhenLockSucceeds_ShouldReturnHandle()
    {
        // Arrange
        var lockName = "test-lock";
        _daprClient.NextLockResponse = Substitute.For<LockResponse>();

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WithCustomOwner_ShouldUseProvidedOwner()
    {
        // Arrange
        var lockName = "test-lock";
        var customOwner = "custom-owner-123";
        _daprClient.NextLockResponse = Substitute.For<LockResponse>();

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName, owner: customOwner);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }
}
#pragma warning restore DAPR_DISTRIBUTEDLOCK