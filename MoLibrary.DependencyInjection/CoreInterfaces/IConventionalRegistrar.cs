using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.DependencyInjection.CoreInterfaces;

public interface IConventionalRegistrar
{
    void AddType(IServiceCollection services, Type type);
}
