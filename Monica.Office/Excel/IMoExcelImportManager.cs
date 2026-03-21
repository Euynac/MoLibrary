using Monica.Office.Excel.Models;

namespace Monica.Office.Excel
{
    /// <summary>
    /// Excel import service
    /// <para>See <see cref="ExcelDemo"/> for an example.</para>
    /// </summary>
    public interface IMoExcelImportManager
    {
        /// <summary>
        /// Imports data
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="filePhysicalPath">The physical path of the Excel file</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(
            string filePhysicalPath,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new();

        /// <summary>
        /// Imports data asynchronously
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="filePhysicalPath">The physical path of the Excel file</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(
              string filePhysicalPath,
              Action<ExcelImportOptions>? optionAction = null
          ) where TImportDto : class, new();

        /// <summary>
        /// Imports data
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileBytes">The Excel file bytes</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(
            byte[] fileBytes,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new();

        /// <summary>
        /// Imports data asynchronously
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileBytes">The Excel file bytes</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(
            byte[] fileBytes,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new();

        /// <summary>
        /// Imports data
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileStream">The Excel file stream</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(
            Stream fileStream,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new();

        /// <summary>
        /// Imports data asynchronously
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileStream">The Excel file stream</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(
            Stream fileStream,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new();
    }
}
