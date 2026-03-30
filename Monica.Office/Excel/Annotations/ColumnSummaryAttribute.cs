using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Annotations
{
    /// <summary>
    /// Excel column statistics attribute (for export only)
    /// <para>1. Apply to properties; multiple instances are allowed.</para>
    /// <para>2. If a property has multiple instances, you must specify the downward row offset with <see cref="OffsetRow"/>.</para>
    /// <para>3. Supported formulas: sum, average, maximum, minimum, and count.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ColumnSummaryAttribute : Attribute
    {
        /// <summary>
        /// Whether to display the label text (default: true)
        /// <para>true: do not display it even when <see cref="Label"/> has a value.</para>
        /// </summary>
        public bool IsShowLabel { get; set; }

        /// <summary>
        /// Label text (displayed on the row above the value)
        /// <para>If null, the default is: header name + formula name.</para>
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Function
        /// <para><see cref="ColumnSummaryFunction"/></para>
        /// </summary>
        public short Function { get; set; }

        /// <summary>
        /// Displays the statistic in a specific property column
        /// <para>If not specified, it is displayed in the current property column.</para>
        /// </summary>
        public string? ShowOnColumnPropertyName { get; set; }

        /// <summary>
        /// Unit
        /// <para>Automatically appended in parentheses after <see cref="Label"/>.</para>
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>
        /// Downward row offset (default: 1)
        /// </summary>
        public int OffsetRow { get; set; } = 1;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ColumnSummaryAttribute()
        {
            IsShowLabel = true;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="function">Function <see cref="ColumnSummaryFunction"/>.</param>
        public ColumnSummaryAttribute(short function) : this()
        {
            Function = function;
        }
    }

    /// <summary>
    /// Function
    /// </summary>
    public enum ColumnSummaryFunction : short
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Sum
        /// </summary>
        [Display(Name = "求和")]
        Sum,
        /// <summary>
        /// Average
        /// </summary>
        [Display(Name = "平均值")]
        Avg,
        /// <summary>
        /// Count
        /// </summary>
        [Display(Name = "计数")]
        Count,
        /// <summary>
        /// Maximum
        /// </summary>
        [Display(Name = "最大值")]
        Max,
        /// <summary>
        /// Minimum
        /// </summary>
        [Display(Name = "最小值")]
        Min
    }
}
