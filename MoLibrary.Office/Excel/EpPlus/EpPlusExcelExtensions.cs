using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Office.Excel.EpPlus.Export;
using MoLibrary.Office.Excel.EpPlus.Import;

namespace MoLibrary.Office.Excel.EpPlus
{
    public static class EpPlusExcelExtensions
    {
        /// <summary>
        /// 使用 EpPlus excel导入导出
        /// </summary>
        /// <param name="services"></param>
        public static void AddEpPlusExcel(this IServiceCollection services)
        {
            services.AddSingleton<IEpPlusCellStyleHandle, EpPlusCellStyleHandle>();
            services.AddSingleton<IEpPlusExcelHandle, EpPlusExcelHandle>();

            services.AddTransient<IExcelImportManager, EpPlusExcelImportProvider>();
            services.AddTransient<IExcelExportManager, EpPlusExcelExportProvider>();
        }
    }
}
