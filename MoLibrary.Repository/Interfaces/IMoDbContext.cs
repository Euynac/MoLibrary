using MoLibrary.Repository.Transaction;

namespace MoLibrary.Repository.Interfaces;

public interface IMoDbContext
{
    void Initialize(IMoUnitOfWork unitOfWork);
}
