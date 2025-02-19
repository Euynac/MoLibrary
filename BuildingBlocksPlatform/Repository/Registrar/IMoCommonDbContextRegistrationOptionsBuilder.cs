using Microsoft.Extensions.DependencyInjection;
namespace BuildingBlocksPlatform.Repository.Registrar;

public interface IMoCommonDbContextRegistrationOptionsBuilder
{
    IServiceCollection Services { get; }
}
