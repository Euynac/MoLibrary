using Monica.Repository.UnitOfWork.Abstractions;

namespace Monica.Repository.Persistence.Abstractions;

public interface IUnitOfWorkAwareDbContext
{
    void Initialize(IUnitOfWork unitOfWork);
}
