using BuildingBlocksPlatform.Transaction;

namespace BuildingBlocksPlatform.Repository.Interfaces;

public interface IMoDbContext
{
    void Initialize(IMoUnitOfWork unitOfWork);
}
