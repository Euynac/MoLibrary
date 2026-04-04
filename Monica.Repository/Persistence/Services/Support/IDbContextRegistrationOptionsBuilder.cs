using Microsoft.Extensions.DependencyInjection;

namespace Monica.Repository.Persistence.Services.Support;

public interface IDbContextRegistrationOptionsBuilder
{
    IServiceCollection Services { get; }
}
