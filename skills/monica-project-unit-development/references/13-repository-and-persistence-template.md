# Repository and Persistence Template

Use `$DomainNamespace$` for the domain project namespace and `$InfrastructureNamespace$` for the repository and persistence namespace chosen by the architecture skill, such as `OrderingService.Infrastructure` or `Domains.Ordering`.

## Use When

- The feature needs persistence access.
- A domain concept needs a repository abstraction.
- A module or service needs a `DbContext` or persistence model update.

## Rules

- Keep the repository interface on the domain side of the boundary.
- Keep the repository implementation on the infrastructure side.
- Use `EfRepository<TDbContext, TEntity, TKey>` for standard EF-backed repositories.
- Put query specialization in repositories, not in handlers.
- Keep `DbContext` focused on persistence structure and mapping.

## Repository Interface

```csharp
using Monica.Repository.Persistence.Abstractions;

namespace $DomainNamespace$.Interfaces;

public interface IRepositoryOrder : IRepository<Order, long>
{
    Task<Order?> FindByNumberAsync(string number, CancellationToken cancellationToken = default);
}
```

## Repository Implementation

```csharp
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services;

namespace $InfrastructureNamespace$.Repository;

public sealed class RepositoryOrder(
    IDbContextProvider<OrderingDbContext> dbContextProvider)
    : EfRepository<OrderingDbContext, Order, long>(dbContextProvider), IRepositoryOrder
{
    public async Task<Order?> FindByNumberAsync(
        string number,
        CancellationToken cancellationToken = default)
    {
        return await FindAsync(x => x.Number == number, cancellationToken: cancellationToken);
    }
}
```

## DbContext

```csharp
using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.Persistence.Services;

namespace $InfrastructureNamespace$.Persistence;

public sealed class OrderingDbContext(
    DbContextOptions<OrderingDbContext> options,
    ICachedServiceProvider serviceProvider)
    : RepositoryDbContext<OrderingDbContext>(options, serviceProvider)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

## Notes

- Keep repository interfaces narrow. Add methods for business-relevant queries, not for every LINQ variation.
- If one aggregate needs eager-loading defaults, centralize them in the repository.
- If persistence mapping becomes substantial, split it into dedicated configuration classes inside the persistence area selected by the architecture skill.
