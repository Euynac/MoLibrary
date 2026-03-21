namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Exported Excel header output.
    /// </summary>
    public class ExcelExportHeaderOutput
    {
        /// <summary>
        /// Displayed header name.
        /// </summary>
        public string HeaderName { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelExportHeaderOutput()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelExportHeaderOutput(string headerName)
        {
            HeaderName = headerName;
        }
    }
}
