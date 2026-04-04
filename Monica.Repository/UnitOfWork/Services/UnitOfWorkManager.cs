using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Services;

public class UnitOfWorkManager(IServiceScopeFactory serviceScopeFactory)
    : IUnitOfWorkManager
{
    public IUnitOfWork? Current => GetCurrentByChecking();
    private readonly AsyncLocal<IUnitOfWork?> _currentUow = new();


    public void SetUnitOfWork(IUnitOfWork? unitOfWork)
    {
        _currentUow.Value = unitOfWork;
    }

    private IUnitOfWork? GetCurrentByChecking()
    {
        var uow = _currentUow.Value;

        //Skip reserved unit of work
        while (uow != null && (uow.IsDisposed || uow.IsCompleted))
        {
            uow = uow.Outer;
        }

        return uow;
    }

    public IUnitOfWork Begin(UnitOfWorkOptions options, bool requiresNew = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        var currentUow = Current;
        if (currentUow != null && !requiresNew)
        {
            return new ChildUnitOfWork(currentUow);
        }

        var unitOfWork = CreateNewUnitOfWork();
        unitOfWork.Initialize(options);

        return unitOfWork;
    }

    public IUnitOfWork Begin(bool requiresNew = false)
    {
        return Begin(new UnitOfWorkOptions(), requiresNew);
    }

    public IUnitOfWork BeginTransaction()
    {
        return Begin(new UnitOfWorkOptions { IsTransactional = true }, true);
    }

    private IUnitOfWork CreateNewUnitOfWork()
    {
        var scope = serviceScopeFactory.CreateScope();
        try
        {
            var outerUow = Current;

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            unitOfWork.SetOuter(outerUow);

            SetUnitOfWork(unitOfWork);

            unitOfWork.OnDisposed(() =>
            {
                SetUnitOfWork(outerUow);
                // ReSharper disable once AccessToDisposedClosure
                scope.Dispose();
            });

            return unitOfWork;
        }
        catch(Exception ex) 
        {
            scope.Dispose();
            ex.ReThrow();
            throw;
        }
    }
}
