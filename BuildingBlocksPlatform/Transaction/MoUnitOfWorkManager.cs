using BuildingBlocksPlatform.Utils;
using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.Transaction;

public class MoUnitOfWorkManager(IServiceScopeFactory serviceScopeFactory)
    : IMoUnitOfWorkManager
{
    public IMoUnitOfWork? Current => GetCurrentByChecking();
    private readonly AsyncLocal<IMoUnitOfWork?> _currentUow = new();


    public void SetUnitOfWork(IMoUnitOfWork? unitOfWork)
    {
        _currentUow.Value = unitOfWork;
    }

    private IMoUnitOfWork? GetCurrentByChecking()
    {
        var uow = _currentUow.Value;

        //Skip reserved unit of work
        while (uow != null && (uow.IsDisposed || uow.IsCompleted))
        {
            uow = uow.Outer;
        }

        return uow;
    }

    public IMoUnitOfWork Begin(MoUnitOfWorkOptions options, bool requiresNew = false)
    {
        Check.NotNull(options, nameof(options));

        var currentUow = Current;
        if (currentUow != null && !requiresNew)
        {
            return new MoChildUnitOfWork(currentUow);
        }

        var unitOfWork = CreateNewUnitOfWork();
        unitOfWork.Initialize(options);

        return unitOfWork;
    }

    public IMoUnitOfWork Begin(bool requiresNew = false)
    {
        return Begin(new MoUnitOfWorkOptions(), requiresNew);
    }

    private IMoUnitOfWork CreateNewUnitOfWork()
    {
        var scope = serviceScopeFactory.CreateScope();
        try
        {
            var outerUow = Current;

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IMoUnitOfWork>();

            unitOfWork.SetOuter(outerUow);

            SetUnitOfWork(unitOfWork);

            unitOfWork.OnDisposed(() =>
            {
                SetUnitOfWork(outerUow);
                scope.Dispose();
            });

            return unitOfWork;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
