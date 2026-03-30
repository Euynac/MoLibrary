namespace Monica.Office.Excel.Services.Support
{
    /// <summary>
    /// Validates Excel file paths and extensions.
    /// </summary>
    internal static class ExcelFileValidator
    {
        private static readonly string[] SupportedExtensions = [".xlsx", ".xls"];

        /// <summary>
        /// Determines whether the provided file name has a supported Excel extension.
        /// </summary>
        /// <param name="fileName">The file name including the extension.</param>
        /// <returns></returns>
        public static bool IsExcel(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var fileExtension = Path.GetExtension(fileName);
            return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates that the provided physical path exists and targets a supported Excel file.
        /// </summary>
        /// <param name="physicalPath">The physical file path.</param>
        public static void Validate(string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                throw new ArgumentNullException(nameof(physicalPath), "文件路径不能为空");
            }

            if (!File.Exists(physicalPath))
            {
                throw new FileNotFoundException($"文件不存在：{physicalPath}");
            }

            if (!IsExcel(physicalPath))
            {
                throw new Exception($"仅支持文件扩展名为 {string.Join(",", SupportedExtensions)} 的文件");
            }
        }
    }
}
