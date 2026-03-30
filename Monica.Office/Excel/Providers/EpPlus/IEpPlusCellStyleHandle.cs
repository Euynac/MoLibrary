using Monica.Office.Excel.Annotations;
using OfficeOpenXml.Style;

namespace Monica.Office.Excel.Providers.EpPlus
{
    /// <summary>
    /// EpPlus cell style handler
    /// </summary>
    internal interface IEpPlusCellStyleHandle
    {
        /// <summary>
        /// Applies the header cell style and font.
        /// </summary>
        /// <param name="cellStyle"></param>
        /// <param name="fontAttr"></param>
        /// <param name="styleAttr"></param>
        /// <returns></returns>
        void SetHeaderCellStyleAndFont(ExcelStyle cellStyle, HeaderStyleAttribute styleAttr,
            HeaderFontAttribute fontAttr);

        /// <summary>
        /// Applies the data cell style and font.
        /// </summary>
        /// <param name="cellStyle"></param>
        /// <param name="styleAttr"></param>
        /// <param name="fontAttr"></param>
        /// <returns></returns>

        void SetDataCellStyleAndFont(ExcelStyle cellStyle, DataStyleAttribute styleAttr,
            DataFontAttribute fontAttr);

        /// <summary>
        /// Applies the header cell style.
        /// </summary>
        void SetHeaderCellStyle(ExcelStyle cellStyle, HeaderStyleAttribute styleAttr);

        /// <summary>
        /// Applies the header cell font.
        /// </summary>
        void SetHeaderCellFont(ExcelFont font, HeaderFontAttribute fontAttr);

        /// <summary>
        /// Applies the data cell style.
        /// </summary>
        void SetDataCellStyle(ExcelStyle cellStyle, DataStyleAttribute styleAttr);

        /// <summary>
        /// Applies the data cell font.
        /// </summary>
        void SetDataCellFont(ExcelFont font, DataFontAttribute fontAttr);
    }
}
