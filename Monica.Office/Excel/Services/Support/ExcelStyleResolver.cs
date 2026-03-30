using System.Reflection;
using Monica.Office.Excel.Annotations;
using Monica.Office.Excel.Models.Internal;

namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Helper for processing Excel style attributes
    /// </summary>
    internal static class ExcelStyleResolver
    {
        ///// <summary>
        ///// Gets the header style and font collection
        ///// </summary>
        ///// <typeparam name="TExportDto"></typeparam>
        ///// <returns></returns>
        //public static List<ExcelCellStyleInfo<HeaderStyleAttribute, HeaderFontAttribute>> GetHeaderStyleFontList<TExportDto>() where TExportDto : class
        //{
        //    var list = new List<ExcelCellStyleInfo<HeaderStyleAttribute, HeaderFontAttribute>>();

        //    foreach (var p in ExcelPropertyResolver.GetProperties<TExportDto>())
        //    {
        //        list.Add(p.GetHeaderStyleFont<TExportDto>());
        //    }

        //    return list;
        //}

        ///// <summary>
        ///// Gets the data style and font collection
        ///// </summary>
        ///// <typeparam name="TExportDto"></typeparam>
        ///// <returns></returns>
        //public static List<ExcelCellStyleInfo<DataStyleAttribute, DataFontAttribute>> GetDataStyleFontList<TExportDto>() where TExportDto : class
        //{
        //    var list = new List<ExcelCellStyleInfo<DataStyleAttribute, DataFontAttribute>>();

        //    foreach (var p in ExcelPropertyResolver.GetProperties<TExportDto>())
        //    {
        //        list.Add(p.GetDataStyleFont<TExportDto>());
        //    }

        //    return list;
        //}

        /// <summary>
        /// Gets the header style and font
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static ExcelCellStyleInfo<HeaderStyleAttribute, HeaderFontAttribute> GetHeaderStyleFont<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var style = m.GetHeaderStyleAttr<TExportDto>();
            var font = m.GetHeaderFontAttr<TExportDto>();

            return new ExcelCellStyleInfo<HeaderStyleAttribute, HeaderFontAttribute>(m, style, font);
        }

        /// <summary>
        /// Gets the data style and font
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static ExcelCellStyleInfo<DataStyleAttribute, DataFontAttribute> GetDataStyleFont<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var style = m.GetDataStyleAttr<TExportDto>();
            var font = m.GetDataFontAttr<TExportDto>();

            return new ExcelCellStyleInfo<DataStyleAttribute, DataFontAttribute>(m, style, font);
        }

        /// <summary>
        /// Gets the header style
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static HeaderStyleAttribute GetHeaderStyleAttr<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var classType = typeof(TExportDto);

            var classStyleAttr = classType.GetCustomAttribute<HeaderStyleAttribute>();
            var styleAttr =m.GetCustomAttribute<HeaderStyleAttribute>();

            return styleAttr ?? classStyleAttr ?? new HeaderStyleAttribute();
        }

        /// <summary>
        /// Gets the header font
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static HeaderFontAttribute GetHeaderFontAttr<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var classType = typeof(TExportDto);

            var classFontAttr = classType.GetCustomAttribute<HeaderFontAttribute>();
            var fontAttr = m.GetCustomAttribute<HeaderFontAttribute>();

            return fontAttr ?? classFontAttr ?? new HeaderFontAttribute();
        }

        /// <summary>
        /// Gets the data style
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static DataStyleAttribute GetDataStyleAttr<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var classType = typeof(TExportDto);

            var classStyleAttr = classType.GetCustomAttribute<DataStyleAttribute>();
            var styleAttr = m.GetCustomAttribute<DataStyleAttribute>();

            return styleAttr ?? classStyleAttr ?? new DataStyleAttribute();
        }

        /// <summary>
        /// Gets the data font
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static DataFontAttribute GetDataFontAttr<TExportDto>(this MemberInfo m) where TExportDto : class
        {
            var classType = typeof(TExportDto);

            var classFontAttr = classType.GetCustomAttribute<DataFontAttribute>();
            var fontAttr = m.GetCustomAttribute<DataFontAttribute>();

            return fontAttr ?? classFontAttr ?? new DataFontAttribute();
        }

        /// <summary>
        /// Determines whether a data font attribute exists
        /// </summary>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static bool HasDataFontAttr(this MemberInfo m) => m.GetCustomAttribute<DataFontAttribute>() != null;

        /// <summary>
        /// Determines whether a data style attribute exists
        /// </summary>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static bool HasDataStyleAttr(this MemberInfo m) => m.GetCustomAttribute<DataStyleAttribute>() != null;

        /// <summary>
        /// Determines whether a header style attribute exists
        /// </summary>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static bool HasHeaderStyleAttr(this MemberInfo m) => m.GetCustomAttribute<HeaderStyleAttribute>() != null;

        /// <summary>
        /// Determines whether a header font attribute exists
        /// </summary>
        /// <param name="m"><c>typeof(Class)</c> or <see cref="PropertyInfo"/></param>
        /// <returns></returns>
        public static bool HasHeaderFontAttr(this MemberInfo m) => m.GetCustomAttribute<HeaderFontAttribute>() != null;
    }
}
