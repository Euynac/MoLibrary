using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Repository.Registrar;

public interface IMoCommonDbContextRegistrationOptionsBuilder
{
    IServiceCollection Services { get; }
}
