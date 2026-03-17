using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Office.Excel;
using Monica.Office.Excel.EpPlus;
using Monica.Office.Excel.EpPlus.Export;
using Monica.Office.Excel.EpPlus.Import;
using Monica.Office.Excel.Npoi;
using Monica.Office.Excel.Npoi.Export;
using Monica.Office.Excel.Npoi.Import;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleExcelBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Excel 模块
        /// </summary>
        public static ModuleExcelGuide AddExcel(Action<ModuleExcelOption>? action = null)
        {
            return new ModuleExcelGuide().Register(action);
        }
    }
}

public class ModuleExcel(ModuleExcelOption option) : MoModule<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Excel;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleProgressBarGuide>().Register();
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
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<INpoiCellStyleHandle, NpoiCellStyleHandle>();
            context.Services.AddSingleton<INpoiExcelHandle, NpoiExcelHandle>();

            context.Services.AddSingleton<IMoExcelImportManager, NpoiExcelImportProvider>();
            context.Services.AddSingleton<IMoExcelExportManager, NpoiExcelExportProvider>();
        }, key: SET_EXCEL_PROVIDER);
        return this;
    }

    /// <summary>
    /// 使用 EpPlus excel导入导出
    /// </summary>
    /// <returns></returns>
    public ModuleExcelGuide UseEpPlusExcel()
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IEpPlusCellStyleHandle, EpPlusCellStyleHandle>();
            context.Services.AddSingleton<IEpPlusExcelHandle, EpPlusExcelHandle>();

            context.Services.AddSingleton<IMoExcelImportManager, EpPlusExcelImportProvider>();
            context.Services.AddSingleton<IMoExcelExportManager, EpPlusExcelExportProvider>();
        }, key: SET_EXCEL_PROVIDER);
        return this;
    }
}

public class ModuleExcelOption : MoModuleOption<ModuleExcel>
{
}