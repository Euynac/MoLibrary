using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.Repository.Registrar;

/// <summary>
/// This is a base class for dbcoUse derived
/// </summary>
public abstract class MoCommonDbContextRegistrationOptions(Type dbContextType, IServiceCollection services) : IMoCommonDbContextRegistrationOptionsBuilder
{
    public Type DbContextType { get; } = dbContextType;

    public IServiceCollection Services { get; } = services;
}
