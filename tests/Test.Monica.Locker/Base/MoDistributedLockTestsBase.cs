using FluentAssertions;
using Microsoft.Extensions.Options;
using Monica.Locker.DistributedLocking;
using Monica.Locker.Modules;
using NUnit.Framework;

namespace Test.Monica.Locker.Base;

public abstract class MoDistributedLockTestsBase
{
    protected readonly IDistributedLockKeyNormalizer KeyNormalizer;
    protected readonly ModuleLockerOption LockOptions;

    protected MoDistributedLockTestsBase()
    {
        LockOptions = new ModuleLockerOption
        {
            KeyPrefix = "test:"
        };
        KeyNormalizer = new DistributedLockKeyNormalizer(Options.Create(LockOptions));
    }

    protected abstract IMoDistributedLock CreateLock();

    [Test]
    public async Task TryAcquireAsync_WithValidName_ShouldAcquireLock()
    {
        // Arrange
        var lockName = "test-lock";
        var @lock = CreateLock();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var lockName = "test-lock";
        var @lock = CreateLock();
        var timeout = TimeSpan.FromMilliseconds(100);

        // Act
        var handle = await @lock.TryAcquireAsync(lockName, timeout: timeout);

        // Assert
        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Test]
    public async Task TryAcquireAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var lockName = "test-lock";
        var @lock = CreateLock();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var handle = await @lock.TryAcquireAsync(lockName, cancellationToken: cts.Token);

        // Assert
        handle.Should().BeNull();
    }

    [Test]
    public async Task TryAcquireAsync_WithDisposedHandle_ShouldReleaseLock()
    {
        // Arrange
        var lockName = "test-lock";
        var @lock = CreateLock();
        var handle = await @lock.TryAcquireAsync(lockName);
        await handle!.DisposeAsync();

        // Act
        var newHandle = await @lock.TryAcquireAsync(lockName);

        // Assert
        newHandle.Should().NotBeNull();
        await newHandle!.DisposeAsync();
    }
}