using Microsoft.Extensions.DependencyInjection;

namespace Monica.Repository.Registrar;

public interface IMoCommonDbContextRegistrationOptionsBuilder
{
    IServiceCollection Services { get; }
}
