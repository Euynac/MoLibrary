using Microsoft.Extensions.DependencyInjection;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// This is a base class for dbcoUse derived
/// </summary>
public abstract class DbContextRegistrationOptionsBase(Type dbContextType, IServiceCollection services) : IDbContextRegistrationOptionsBuilder
{
    public Type DbContextType { get; } = dbContextType;

    public IServiceCollection Services { get; } = services;
}
