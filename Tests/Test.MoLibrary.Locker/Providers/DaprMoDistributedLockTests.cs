using Dapr.Client;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Dapr;
using NSubstitute;
using NUnit.Framework;
using Test.MoLibrary.Locker.Base;

namespace Test.MoLibrary.Locker.Providers;

public class DaprMoDistributedLockTests : MoDistributedLockTestsBase
{
    private readonly DaprClient _daprClient;
    private readonly MoDistributedLockDaprOptions _daprOptions;

    public DaprMoDistributedLockTests()
    {
        _daprClient = Substitute.For<DaprClient>();
        _daprOptions = new MoDistributedLockDaprOptions
        {
            StoreName = "test-store",
            Owner = "test-owner",
            DefaultExpirationTimeout = TimeSpan.FromMinutes(5)
        };
    }

    protected override IMoDistributedLock CreateLock()
    {
        return new DaprMoDistributedLock(
            _daprClient,
            Options.Create(_daprOptions),
            KeyNormalizer
        );
    }

    [Test]
    public async Task TryAcquireAsync_ShouldUseNormalizedKey()
    {
        // Arrange
        var lockName = "test-lock";
        var normalizedKey = KeyNormalizer.NormalizeKey(lockName);
        var mockResponse = new TryLockResponse { Success = true };
        _daprClient.Lock(
            _daprOptions.StoreName,
            normalizedKey,
            _daprOptions.Owner,
            (int)_daprOptions.DefaultExpirationTimeout.TotalSeconds,
            Arg.Any<CancellationToken>()
        ).Returns(mockResponse);

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await _daprClient.Received(1).Lock(
            _daprOptions.StoreName,
            normalizedKey,
            _daprOptions.Owner,
            (int)_daprOptions.DefaultExpirationTimeout.TotalSeconds,
            Arg.Any<CancellationToken>()
        );
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WhenLockFails_ShouldReturnNull()
    {
        // Arrange
        var lockName = "test-lock";
        var mockResponse = new TryLockResponse { Success = false };
        _daprClient.Lock(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        ).Returns(mockResponse);

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().BeNull();
    }

    [Test]
    public async Task TryAcquireAsync_WhenLockSucceeds_ShouldReturnHandle()
    {
        // Arrange
        var lockName = "test-lock";
        var mockResponse = new TryLockResponse { Success = true };
        _daprClient.Lock(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>()
        ).Returns(mockResponse);

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }
} 