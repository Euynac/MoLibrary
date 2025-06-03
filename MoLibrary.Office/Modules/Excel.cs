using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Office.Excel.Npoi.Export;
using MoLibrary.Office.Excel.Npoi.Import;
using MoLibrary.Office.Excel.Npoi;
using MoLibrary.Office.Excel;
using MoLibrary.Tool.MoResponse;
using MoLibrary.Office.Excel.EpPlus.Export;
using MoLibrary.Office.Excel.EpPlus.Import;
using MoLibrary.Office.Excel.EpPlus;

namespace MoLibrary.Office.Modules;


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

/// <summary>
/// Excel模块配置指南
/// </summary>
public class ModuleExcelGuide : MoModuleGuide<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>
{
    private const string SET_EXCEL_PROVIDER = nameof(SET_EXCEL_PROVIDER);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_EXCEL_PROVIDER];
    }
    /// <summary>
    /// 使用 NPOI excel导入导出
    /// </summary>
    /// <returns></returns>
    public ModuleExcelGuide UseNpoiExcel()
    {
        ConfigureServices(SET_EXCEL_PROVIDER, context =>
        {
            context.Services.AddSingleton<INpoiCellStyleHandle, NpoiCellStyleHandle>();
            context.Services.AddSingleton<INpoiExcelHandle, NpoiExcelHandle>();

            context.Services.AddTransient<IExcelImportManager, NpoiExcelImportProvider>();
            context.Services.AddTransient<IExcelExportManager, NpoiExcelExportProvider>();
        });
        return this;
    }

    /// <summary>
    /// 使用 EpPlus excel导入导出
    /// </summary>
    /// <returns></returns>
    public ModuleExcelGuide UseEpPlusExcel()
    {
        ConfigureServices(SET_EXCEL_PROVIDER, context =>
        {
            context.Services.AddSingleton<IEpPlusCellStyleHandle, EpPlusCellStyleHandle>();
            context.Services.AddSingleton<IEpPlusExcelHandle, EpPlusExcelHandle>();

            context.Services.AddTransient<IExcelImportManager, EpPlusExcelImportProvider>();
            context.Services.AddTransient<IExcelExportManager, EpPlusExcelExportProvider>();
        });
        return this;
    }
}

public class ModuleExcelOption : MoModuleOption<ModuleExcel>
{
}