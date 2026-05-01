# Repository and DbContext Template

Use `$DomainNamespace$` for the domain project namespace and `$RepositoryNamespace$` for the repository namespace chosen by the architecture skill, such as `OrderingService.Domain` or `Domains.Ordering`.

## Use When

- The feature needs persistence access.
- A domain concept needs a repository abstraction.
- A module or service needs a `DbContext` or persistence model update.

## Rules

- Keep the repository interface on the domain side of the boundary.
- Keep the repository implementation on the owning domain-side infrastructure boundary: `{Subdomain}Service.Domain/Repository/` for microservices and `Domains/{Subdomain}/Repository/` for modular monolith domains.
- Keep libraries that only this repository or adapter needs in the owning subdomain/service project. Do not move the implementation to shared `Platform` only because it uses a third-party package.
- Use `EfRepository<TDbContext, TEntity, TKey>` for standard EF-backed repositories.
- Put query specialization in repositories, not in handlers.
- Keep `DbContext` focused on persistence structure and mapping.
- Do not create a separate `Persistence/` folder by default. Keep repository implementations, `DbContext`, and EF mapping together under `Repository/`.

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

namespace $RepositoryNamespace$.Repository;

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

namespace $RepositoryNamespace$.Repository;

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
- Keep EF mapping files beside the `DbContext` inside `Repository/`, using explicit type names instead of another folder layer.
