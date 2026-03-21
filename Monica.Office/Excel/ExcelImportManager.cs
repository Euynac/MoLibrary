using Monica.Office.Excel.Models;

namespace Monica.Office.Excel
{
    /// <summary>
    /// Excel import service
    /// </summary>
    public abstract class ExcelImportManager : IMoExcelImportManager
    {
        /// <summary>
        /// Initializes a new instance
        /// </summary>
        protected ExcelImportManager()
        {
        }

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
        public List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(string filePhysicalPath, Action<ExcelImportOptions>? optionAction = null) where TImportDto : class, new()
        {
            try
            {
                ExcelHelper.ValidationExcel(filePhysicalPath);

                using var stream = new FileStream(filePhysicalPath, FileMode.Open, FileAccess.Read);
                return Import<TImportDto>(stream, optionAction);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
        }

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
        public Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(string filePhysicalPath, Action<ExcelImportOptions>? optionAction = null) where TImportDto : class, new()
        {
            return Task.FromResult(Import<TImportDto>(filePhysicalPath, optionAction));
        }

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
        public List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(byte[] fileBytes, Action<ExcelImportOptions>? optionAction = null) where TImportDto : class, new()
        {
            try
            {
                using var stream = new MemoryStream(fileBytes);
                return Import<TImportDto>(stream, optionAction);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }

        }


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
        public Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(byte[] fileBytes, Action<ExcelImportOptions>? optionAction = null) where TImportDto : class, new()
        {
            return Task.FromResult(Import<TImportDto>(fileBytes, optionAction));
        }

        /// <summary>
        /// Imports data
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileStream">The file stream</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        public List<ExcelSheetDataOutput<TImportDto>> Import<TImportDto>(
            Stream fileStream,
            Action<ExcelImportOptions>? optionAction = null
        ) where TImportDto : class, new()
        {
            try
            {
                return ImplementImport<TImportDto>(fileStream, optionAction);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }

        }

        /// <summary>
        /// Imports data asynchronously
        /// </summary>
        /// <typeparam name="TImportDto">The DTO type mapped to the header row
        /// <para>1. Each header cell name maps to the <c>DisplayName</c> attribute in <see cref="System.ComponentModel.DataAnnotations"/>.</para>
        /// <para>2. Field validation can use any attribute in <see cref="System.ComponentModel.DataAnnotations"/>, such as Required, StringLength, Range, RegularExpression, EnumDataType, and DefaultValue.</para>
        /// </typeparam>
        /// <param name="fileStream">The file stream</param>
        /// <param name="optionAction">Configures import options</param>
        /// <returns></returns>
        public Task<List<ExcelSheetDataOutput<TImportDto>>> ImportAsync<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction = null) where TImportDto : class, new()
        {
            return Task.FromResult(Import<TImportDto>(fileStream, optionAction));
        }

        /// <summary>
        /// Implements the import operation
        /// </summary>
        /// <typeparam name="TImportDto"></typeparam>
        /// <param name="fileStream"></param>
        /// <param name="optionAction"></param>
        /// <returns></returns>
        protected abstract List<ExcelSheetDataOutput<TImportDto>> ImplementImport<TImportDto>(Stream fileStream, Action<ExcelImportOptions>? optionAction) where TImportDto : class, new();

    }
}
