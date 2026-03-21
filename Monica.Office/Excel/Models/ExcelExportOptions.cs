using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel export options.
    /// </summary>
    public class ExcelExportOptions
    {
        /// <summary>
        /// Worksheet name. Default: Sheet1.
        /// </summary>
        [Display(Name = "工作表名称")]
        [StringLength(30, ErrorMessage = "{0}最大长度为{1}")]
        public string SheetName { get; set; }

        /// <summary>
        /// Header row index. Default: 1.
        /// <para>One-based index.</para>
        /// </summary>
        [Display(Name = "表头行编号")]
        [Range(1, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int HeaderRowIndex { get; set; }

        /// <summary>
        /// Data start row index. Default: 2.
        /// <para>One-based index.</para>
        /// </summary>
        [Display(Name = "数据起始行编号")]
        [Range(2, int.MaxValue, ErrorMessage = "{0}最小值为{1}")]
        public int DataRowStartIndex { get; set; }

        /// <summary>
        /// File format. Default: Xlsx.
        /// </summary>
        [Display(Name = "文件格式")]
        [EnumDataType(typeof(ExcelTypeEnum), ErrorMessage = "{0}值不存在")]
        public ExcelTypeEnum ExcelType { get; set; }

        /// <summary>
        /// Checks whether <see cref="ExcelHeaderRequest.QueryName"/> contains duplicate columns. Exporting the same column more than once is not allowed.
        /// </summary>
        public bool DisallowDuplicateHeader { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelExportOptions()
        {
            SheetName = "Sheet1";
            HeaderRowIndex = 1;
            DataRowStartIndex = 2;
            ExcelType = ExcelTypeEnum.Xlsx;
        }

        /// <summary>
        /// Validates the options.
        /// </summary>
        public void CheckError()
        {
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
    /// Excel file type.
    /// </summary>
    public enum ExcelTypeEnum
    {
        /// <summary>
        /// Xlsx
        /// <para>Excel version &gt;= 2007.</para>
        /// <para>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</para>
        /// </summary>
        Xlsx,
        /// <summary>
        /// Xls
        /// <para>Excel version &lt;= 2003.</para>
        /// <para>application/vnd.ms-excel</para>
        /// </summary>
        Xls,
    }
}
