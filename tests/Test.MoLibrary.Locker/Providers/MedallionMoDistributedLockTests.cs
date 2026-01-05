using FluentAssertions;
using Medallion.Threading;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Medallion;
using NSubstitute;
using NUnit.Framework;
using Test.MoLibrary.Locker.Base;

namespace Test.MoLibrary.Locker.Providers;

public class MedallionMoDistributedLockTests : MoDistributedLockTestsBase
{
    private readonly IDistributedLockProvider _distributedLockProvider;

    public MedallionMoDistributedLockTests()
    {
        _distributedLockProvider = Substitute.For<IDistributedLockProvider>();
    }

    protected override IMoDistributedLock CreateLock()
    {
        return new MedallionMoDistributedLock(_distributedLockProvider, KeyNormalizer);
    }

    [Test]
    public async Task TryAcquireAsync_ShouldUseNormalizedKey()
    {
        // Arrange
        var lockName = "test-lock";
        var normalizedKey = KeyNormalizer.NormalizeKey(lockName);
        var mockHandle = Substitute.For<IDistributedSynchronizationHandle>();
        _distributedLockProvider.TryAcquireLockAsync(
            normalizedKey,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()
        ).Returns(mockHandle);

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await _distributedLockProvider.Received(1).TryAcquireLockAsync(
            normalizedKey,
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()
        );
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WhenProviderReturnsNull_ShouldReturnNull()
    {
        // Arrange
        var lockName = "test-lock";
        _distributedLockProvider.TryAcquireLockAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()
        ).Returns((IDistributedSynchronizationHandle?)null);

        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().BeNull();
    }
}