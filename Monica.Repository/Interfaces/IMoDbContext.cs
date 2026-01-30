using Monica.Repository.Transaction;

namespace Monica.Repository.Interfaces;

public interface IMoDbContext
{
    void Initialize(IMoUnitOfWork unitOfWork);
}
