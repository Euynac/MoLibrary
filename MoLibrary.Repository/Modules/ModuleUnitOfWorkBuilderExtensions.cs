using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Repository.Modules;


public static class ModuleUnitOfWorkBuilderExtensions
{
    public static ModuleUnitOfWorkGuide AddMoModuleUnitOfWork(this WebApplicationBuilder builder,
        Action<ModuleUnitOfWorkOption>? action = null)
    {
        return new ModuleUnitOfWorkGuide().Register(action);
    }
}