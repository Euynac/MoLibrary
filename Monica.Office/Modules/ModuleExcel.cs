using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Office.Excel.Abstractions;
using Monica.Office.Excel.Providers.EpPlus;
using Monica.Office.Excel.Providers.Npoi;

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

[ModuleKey(BuiltInModuleKey.Excel)]
public class ModuleExcel(ModuleExcelOption option) : ModuleBase<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>(option)
{

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleTaskProgressGuide>().Register();
    }
}

/// <summary>
/// Excel module configuration guide
/// </summary>
public class ModuleExcelGuide : ModuleGuide<ModuleExcel, ModuleExcelOption, ModuleExcelGuide>
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
            context.Services.AddSingleton<INpoiCellStyleSupport, NpoiCellStyleSupport>();
            context.Services.AddSingleton<INpoiWorkbookSupport, NpoiWorkbookSupport>();

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
            context.Services.AddSingleton<IEpPlusCellStyleSupport, EpPlusCellStyleSupport>();
            context.Services.AddSingleton<IEpPlusWorkbookSupport, EpPlusWorkbookSupport>();

            context.Services.AddSingleton<IExcelImporter, EpPlusExcelImportProvider>();
            context.Services.AddSingleton<IExcelExporter, EpPlusExcelExportProvider>();
        }, key: SET_EXCEL_PROVIDER);
        return this;
    }
}

public class ModuleExcelOption : ModuleOptions<ModuleExcel>
{
}
