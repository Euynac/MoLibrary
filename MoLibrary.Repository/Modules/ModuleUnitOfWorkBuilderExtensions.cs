using Microsoft.AspNetCore.Builder;

namespace MoLibrary.Repository.Modules;


public static class ModuleUnitOfWorkBuilderExtensions
{
    public static ModuleUnitOfWorkGuide ConfigModuleUnitOfWork(this WebApplicationBuilder builder,
        Action<ModuleUnitOfWorkOption>? action = null)
    {
        return new ModuleUnitOfWorkGuide().Register(action);
    }
}