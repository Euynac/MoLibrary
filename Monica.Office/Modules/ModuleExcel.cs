using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Office.Excel;
using Monica.Office.Excel.Abstractions;
using Monica.Office.Excel.Providers.EpPlus;
using Monica.Office.Excel.Providers.EpPlus.Export;
using Monica.Office.Excel.Providers.EpPlus.Import;
using Monica.Office.Excel.Providers.Npoi;
using Monica.Office.Excel.Providers.Npoi.Export;
using Monica.Office.Excel.Providers.Npoi.Import;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExcelBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Excel module
        /// </summary>
        public static ModuleExcelGuide AddExcel(Action<ModuleExcelOption>? action = null)
        {
            return new ModuleExcelGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Excel)]
public class ModuleExcel(ModuleExcelOption option) : MoModule<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleProgressBarGuide>().Register();
    }
}

/// <summary>
/// Excel module configuration guide
/// </summary>
public class ModuleExcelGuide : MoModuleGuide<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>
{
    private const string SET_EXCEL_PROVIDER = nameof(SET_EXCEL_PROVIDER);
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [SET_EXCEL_PROVIDER];
    }
    /// <summary>
    /// Uses NPOI for Excel import and export
    /// </summary>
    /// <returns></returns>
    public ModuleExcelGuide UseNpoi()
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<INpoiCellStyleHandle, NpoiCellStyleHandle>();
            context.Services.AddSingleton<INpoiExcelHandle, NpoiExcelHandle>();

            context.Services.AddSingleton<IExcelImporter, NpoiExcelImportProvider>();
            context.Services.AddSingleton<IExcelExporter, NpoiExcelExportProvider>();
        }, key: SET_EXCEL_PROVIDER);
        return this;
    }

    /// <summary>
    /// Uses EpPlus for Excel import and export
    /// </summary>
    /// <returns></returns>
    public ModuleExcelGuide UseEpPlus()
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton<IEpPlusCellStyleHandle, EpPlusCellStyleHandle>();
            context.Services.AddSingleton<IEpPlusExcelHandle, EpPlusExcelHandle>();

            context.Services.AddSingleton<IExcelImporter, EpPlusExcelImportProvider>();
            context.Services.AddSingleton<IExcelExporter, EpPlusExcelExportProvider>();
        }, key: SET_EXCEL_PROVIDER);
        return this;
    }
}

public class ModuleExcelOption : MoModuleOption<ModuleExcel>
{
}
