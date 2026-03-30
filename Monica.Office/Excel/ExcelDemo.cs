using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Monica.Office.Excel.Abstractions;
using Monica.Office.Excel.Annotations;
using Monica.Office.Excel.Models;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;

namespace Monica.Office.Excel
{
    /// <summary>
    /// Excel import example
    /// </summary>
    /// <remarks>
    /// Initializes a new instance
    /// </remarks>
    internal class ExcelDemo(IExcelImporter excelImportManager, IExcelExporter excelExportManager)
    {
        /// <summary>
        /// Import test
        /// </summary>
        /// <returns></returns>
        public async Task Import(Stream stream)
        {
            try
            {
                // Import
                var data = await excelImportManager.ImportAsync<ImportTest>(stream, opt =>
                {
                    opt.SheetIndex = 0;
                    opt.ValidateMode = ExcelValidateModeEnum.ThrowRow;
                });

                // Get valid data
                var valid = data.GetValidData();

                // Get invalid data
                var invalid = data.GetInvalidData();

                // Get all data
                var all = data.GetAllData();

                // Get the error message. Returns null when there are no errors.
                var error = data.GetErrorMessage();

                // Validate errors and throw when necessary
                data.CheckError();
            }
            catch (Exception)
            {
                // Return the error message: e.Message
            }
        }

        /// <summary>
        /// Export test
        /// </summary>
        public async Task<byte[]> Export()
        {
            try
            {
                var list = new List<ExportTest>();
                var now = DateTime.Now.Date;
                var random = new Random();
                for (var i = 0; i < 11; i++)
                {
                    list.Add(new ExportTest
                    {
                        Name = "张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三张三" + random.Next(1, 3),
                        Name1 = "张三" + random.Next(1, 3),
                        Name11 = "张三" + random.Next(1, 3),
                        Age = random.Next(10, 50),
                        Score = random.Next(1000, 5000),
                        Edu = (TestEnum)random.Next(1, 4),
                        Date = now.AddDays(random.Next(1, 3)),
                        Date1 = now.AddDays(random.Next(1, 3))
                    });
                }
                var bytes = await excelExportManager.ExportAsync(list, opt =>
                    {
                        opt.SheetName = "sheet名称";
                    }, ["姓名", "日期", "年龄", "成绩"]
                );

                return bytes;
            }
            catch (Exception)
            {
                // Return the error message: e.Message
                throw;
            }
        }

        /// <summary>
        /// Import DTO
        /// </summary>
        public class ImportTest
        {
            /// <summary>
            /// Name
            /// </summary>
            [Display(Name = "姓名")]
            [Required(ErrorMessage = "{0}不能为空")]
            [StringLength(4, ErrorMessage = "{0}最大长度为{1}")]
            public virtual string? Name { get; set; }

            /// <summary>
            /// Phone number
            /// </summary>
            [Display(Name = "手机号")]
            [RegularExpression(@"^1[3456789]\d{9}$", ErrorMessage = "{0}格式错误")]
            public virtual string? Phone { get; set; }

            /// <summary>
            /// Age
            /// </summary>
            [Display(Name = "年龄")]
            [Required(ErrorMessage = "{0}不能为空")]
            [Range(10, 100, ErrorMessage = "{0}区间为{1}~{2}")]
            public virtual int Age { get; set; }

            /// <summary>
            /// Score
            /// </summary>
            [Display(Name = "成绩")]
            [Range(0, 150, ErrorMessage = "{0}区间为{1}~{2}")]
            public virtual decimal? Score { get; set; }

            /// <summary>
            /// Date
            /// </summary>
            [Display(Name = "日期")]
            [DefaultValue(typeof(DateTime), "2020-9-9")]
            public virtual DateTime Date { get; set; }

            /// <summary>
            /// Time
            /// </summary>
            [Display(Name = "时间")]
            [DefaultValue(typeof(TimeSpan), "100.10:20:30")]
            public virtual TimeSpan Time { get; set; }

            /// <summary>
            /// Education
            /// </summary>
            [Display(Name = "学历")]
            [EnumDataType(typeof(TestEnum), ErrorMessage = "{0}值不存在")]
            public virtual TestEnum? Edu { get; set; }

            /// <summary>
            /// Education text (ignored)
            /// </summary>
            [Display(Name = "学历文本")]
            [IgnoreColumn]
            public virtual string? EduText => Edu?.ToString();

            /// <summary>
            /// No DisplayName
            /// </summary>
            public virtual string? NoDisplayName { get; set; }
        }

        public enum TestEnum
        {
            小学 = 1,
            中学,
            大学
        }

        /// <summary>
        /// Export DTO
        /// </summary>
        [HeaderStyle(ColumnAutoSize = true)]
        [RowHeight(20, 30)]
        [HeaderFont(Color = 14)]
        [DataStyle(FillPattern = (short)FillPattern.SolidForeground, FillForegroundColor = HSSFColor.LightOrange.Index)]
        [DataFont(Color = 16)]
        [MergeColumn(nameof(Name1), nameof(Name11))]
        [MergeColumn(nameof(Date), nameof(Date1))]
        public class ExportTest
        {
            /// <summary>
            /// Name
            /// </summary>
            [Display(Name = "姓名")]
            [DataStyle(WrapText = true, FillPattern = (short)FillPattern.SolidForeground, FillForegroundColor = HSSFColor.Green.Index)]
            [DataFont(Color = 10)]
            [MergeRow]
            public virtual string? Name { get; set; }

            /// <summary>
            /// Name 1
            /// </summary>
            [Display(Name = "姓名1")]
            [HeaderStyle(ColumnSize = 40)]
            [DataFont(FontHeightInPoints = 15)]
            public virtual string? Name1 { get; set; }

            /// <summary>
            /// Name 2
            /// </summary>
            [Display(Name = "姓名11")]
            [DataFont(FontHeightInPoints = 18)]
            public virtual string? Name11 { get; set; }

            /// <summary>
            /// Date
            /// </summary>
            [Display(Name = "日期")]
            [DataStyle(DataFormat = "yyyy\"年\"m\"月\"d\"日\";@")]
            [ColumnSummary((int)ColumnSummaryFunction.Avg)]
            public virtual DateTime? Date { get; set; }

            /// <summary>
            /// Date 2
            /// </summary>
            [Display(Name = "日期2")]
            [DefaultValue(typeof(DateTime), "2020-9-9")]
            public virtual DateTime? Date1 { get; set; }

            /// <summary>
            /// Age
            /// </summary>
            [Display(Name = "年龄")]
            [ColumnSummary((int)ColumnSummaryFunction.Avg)]
            public virtual int Age { get; set; }

            /// <summary>
            /// Score
            /// </summary>
            [Display(Name = "成绩")]
            [HeaderFont(Color = 15)]
            [DataStyle(DataFormat = "#,##0.00_ ")]
            [ColumnSummary((int)ColumnSummaryFunction.Avg, OffsetRow = 4)]
            [ColumnSummary((int)ColumnSummaryFunction.Sum)]
            public virtual decimal? Score { get; set; }

            /// <summary>
            /// Pass status
            /// </summary>
            [Display(Name = "是否及格")]
            public virtual bool IsLoanCar => Score > 3000;

            /// <summary>
            /// Education
            /// </summary>
            [Display(Name = "学历")]
            [IgnoreColumn]
            public virtual TestEnum? Edu { get; set; }

            /// <summary>
            /// Education text
            /// </summary>
            [Display(Name = "学历文本")]
            public virtual string? EduText => Edu?.ToString();

            /// <summary>
            /// Time
            /// </summary>
            [Display(Name = "时间")]
            public virtual TimeSpan Time => TimeSpan.FromDays(1);
        }
    }
}
