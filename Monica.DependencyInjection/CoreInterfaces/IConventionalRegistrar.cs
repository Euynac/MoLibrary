using Microsoft.Extensions.DependencyInjection;

namespace Monica.DependencyInjection.CoreInterfaces;

public interface IConventionalRegistrar
{
    void AddType(IServiceCollection services, Type type);
}
