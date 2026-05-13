using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Services;

/// <summary>
/// Default ambient unit-of-work manager.
/// </summary>
public class UnitOfWorkManager(IServiceScopeFactory serviceScopeFactory)
    : IUnitOfWorkManager
{
    private readonly AsyncLocal<IUnitOfWork?> _currentUow = new();

    public IUnitOfWork? Current => GetCurrentByChecking();

    public IUnitOfWork BeginScope(UnitOfWorkScopeOptions? options = null)
    {
        var scopeOptions = options ?? new UnitOfWorkScopeOptions();
        var currentUow = Current;
        if (currentUow != null && !scopeOptions.RequiresNew)
        {
            return new ChildUnitOfWork(currentUow);
        }

        return CreateNewUnitOfWork(scopeOptions);
    }

    public async Task RunAsync(
        Func<Task> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var unitOfWork = BeginScope(options);
        try
        {
            await work();
            await unitOfWork.CompleteAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> RunAsync<T>(
        Func<Task<T>> work,
        UnitOfWorkScopeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var unitOfWork = BeginScope(options);
        try
        {
            var result = await work();
            await unitOfWork.CompleteAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public void SetUnitOfWork(IUnitOfWork? unitOfWork)
    {
        _currentUow.Value = unitOfWork;
    }

    private IUnitOfWork? GetCurrentByChecking()
    {
        var unitOfWork = _currentUow.Value;

        while (unitOfWork is IUnitOfWorkInternals { IsDisposed: true } or { IsCompleted: true })
        {
            unitOfWork = ((IUnitOfWorkInternals)unitOfWork).Outer;
        }

        return unitOfWork;
    }

    private IUnitOfWork CreateNewUnitOfWork(UnitOfWorkScopeOptions options)
    {
        var scope = serviceScopeFactory.CreateScope();
        try
        {
            var outerUow = Current;
            var unitOfWork = ActivatorUtilities.CreateInstance<UnitOfWork>(
                scope.ServiceProvider,
                options);

            ((IUnitOfWorkInternals)unitOfWork).SetOuter(outerUow);

            SetUnitOfWork(unitOfWork);

            ((IUnitOfWorkInternals)unitOfWork).OnDisposed(() =>
            {
                SetUnitOfWork(outerUow);
                scope.Dispose();
            });

            return unitOfWork;
        }
        catch (Exception exception)
        {
            scope.Dispose();
            exception.ReThrow();
            throw;
        }
    }
}
