using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Excel.Modules;


public static class ModuleExcelBuilderExtensions
{
    public static ModuleExcelGuide ConfigModuleExcel(this WebApplicationBuilder builder,
        Action<ModuleExcelOption>? action = null)
    {
        return new ModuleExcelGuide().Register(action);
    }
}

public class ModuleExcel(ModuleExcelOption option) : MoModule<ModuleExcel, ModuleExcelOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Excel;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
}

public class ModuleExcelGuide : MoModuleGuide<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>
{


}

public class ModuleExcelOption : MoModuleOption<ModuleExcel>
{
}