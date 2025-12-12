using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Repository.Interfaces;

namespace MoLibrary.Repository.Modules;

public class ModuleUnitOfWorkGuide : MoModuleGuide<ModuleUnitOfWork, ModuleUnitOfWorkOption, ModuleUnitOfWorkGuide>
{
    public ModuleUnitOfWorkGuide AddDbContextProvider<TDbContext>() where TDbContext : MoDbContext<TDbContext>
    {
        ConfigureServices(context =>
        {
            context.Services.AddTransient(typeof(IDbContextProvider<TDbContext>), typeof(UnitOfWorkDbContextProvider<TDbContext>));
            //TODO 可使用Singleton？
        }, secondKey: nameof(TDbContext));
        return this;
    }

}