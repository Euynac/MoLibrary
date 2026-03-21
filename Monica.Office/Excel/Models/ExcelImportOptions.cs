using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel import options.
    /// </summary>
    public class ExcelImportOptions
    {
        /// <summary>
        /// Worksheet index (default: 1)
        /// <para>Use a one-based index when selecting a specific worksheet.</para>
        /// <para>0: all worksheets, 1: first worksheet, 2: second worksheet, and so on.</para>
        /// </summary>
        [Display(Name = "工作表编号")]
        [Range(0, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int SheetIndex { get; set; }
        /// <summary>
        /// Header row index (default: 1)
        /// <para>One-based index.</para>
        /// </summary>
        [Display(Name = "表头行编号")]
        [Range(1, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int HeaderRowIndex { get; set; }

        /// <summary>
        /// Data start row index (default: 2)
        /// <para>One-based index.</para>
        /// </summary>
        [Display(Name = "数据起始行编号")]
        [Range(2, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int DataRowStartIndex { get; set; }

        /// <summary>
        /// Data end row index (default: last row)
        /// <para>One-based index.</para>
        /// </summary>
        [Display(Name = "数据结束行编号")]
        [Range(2, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int? DataRowEndIndex { get; set; }

        /// <summary>
        /// Validation mode (default: after reading the entire worksheet, throw an exception if invalid data exists)
        /// </summary>
        [Display(Name = "数据校验模式")]
        [EnumDataType(typeof(ExcelValidateModeEnum), ErrorMessage = "{0}值不存在")]
        public ExcelValidateModeEnum ValidateMode { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelImportOptions()
        {
            SheetIndex = 1;
            HeaderRowIndex = 1;
            DataRowStartIndex = 2;
            DataRowEndIndex = null;
            ValidateMode = ExcelValidateModeEnum.ThrowSheet;
        }

        /// <summary>
        /// Validates the options.
        /// </summary>
        public void CheckError()
        {
            if (DataRowEndIndex < DataRowStartIndex)
            {
                throw new Exception("【数据结束行编号】不能小于【数据起始行编号】");
            }
            if (DataRowStartIndex <= HeaderRowIndex)
            {
                throw new Exception("【表头行编号】必须小于【数据起始行编号】");
            }
            var valid = new List<ValidationResult>();
            var success = Validator.TryValidateObject(this, new ValidationContext(this), valid, true);
            if (!success)
            {
                throw new Exception(valid[0].ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Handling mode when data validation fails.
    /// </summary>
    public enum ExcelValidateModeEnum
    {
        /// <summary>
        /// After reading a row, stop if invalid data exists.
        /// </summary>
        StopRow,

        /// <summary>
        /// After reading a row, throw an exception if invalid data exists.
        /// </summary>
        ThrowRow,

        /// <summary>
        /// After reading the entire worksheet, stop if invalid data exists.
        /// </summary>
        StopSheet,

        /// <summary>
        /// After reading the entire worksheet, throw an exception if invalid data exists.
        /// </summary>
        ThrowSheet,

        /// <summary>
        /// After reading all worksheets, do not throw exceptions.
        /// </summary>
        ReadBook,

        /// <summary>
        /// After reading all worksheets, throw all exceptions if invalid data exists.
        /// </summary>
        ThrowBook
    }
}
