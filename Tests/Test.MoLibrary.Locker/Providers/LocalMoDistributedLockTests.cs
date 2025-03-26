using FluentAssertions;
using MoLibrary.Locker.DistributedLocking;
using MoLibrary.Locker.Providers.Local;
using NUnit.Framework;
using Test.MoLibrary.Locker.Base;

namespace Test.MoLibrary.Locker.Providers;

public class LocalMoDistributedLockTests : MoDistributedLockTestsBase
{
    protected override IMoDistributedLock CreateLock()
    {
        return new LocalMoDistributedLock(KeyNormalizer);
    }

    [Test]
    public async Task TryAcquireAsync_WithConcurrentAccess_ShouldPreventConcurrentExecution()
    {
        // Arrange
        var lockName = "test-lock";
        var @lock = CreateLock();
        var task1 = @lock.TryAcquireAsync(lockName);
        var task2 = @lock.TryAcquireAsync(lockName);

        // Act
        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Count(r => r != null).Should().Be(1);
        foreach (var handle in results.Where(r => r != null))
        {
            await handle!.DisposeAsync();
        }
    }
}