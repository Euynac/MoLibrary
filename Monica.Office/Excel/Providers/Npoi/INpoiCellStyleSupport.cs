using Monica.Office.Excel.Annotations;
using NPOI.SS.UserModel;

namespace Monica.Office.Excel.Providers.Npoi
{
    /// <summary>
    /// NPOI cell style support.
    /// </summary>
    internal interface INpoiCellStyleSupport
    {
        /// <summary>
        /// Applies the header cell style and font.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="fontAttr"></param>
        /// <param name="styleAttr"></param>
        /// <returns></returns>
        ICellStyle SetHeaderCellStyleAndFont(IWorkbook workbook, HeaderStyleAttribute styleAttr,
            HeaderFontAttribute fontAttr);


        /// <summary>
        /// Applies the data cell style and font.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="styleAttr"></param>
        /// <param name="fontAttr"></param>
        /// <returns></returns>

        ICellStyle SetDataCellStyleAndFont(IWorkbook workbook, DataStyleAttribute styleAttr,
            DataFontAttribute fontAttr);

        /// <summary>
        /// Creates the header cell style.
        /// </summary>
        ICellStyle CreateHeaderCellStyle(IWorkbook workbook, HeaderStyleAttribute styleAttr);

        /// <summary>
        /// Creates the header cell font.
        /// </summary>
        IFont CreateHeaderCellFont(IWorkbook workbook, HeaderFontAttribute fontAttr);

        /// <summary>
        /// Creates the data cell style.
        /// </summary>
        ICellStyle CreateDataCellStyle(IWorkbook workbook, DataStyleAttribute styleAttr);

        /// <summary>
        /// Creates the data cell font.
        /// </summary>
        IFont CreateDataCellFont(IWorkbook workbook, DataFontAttribute fontAttr);
    }
}
