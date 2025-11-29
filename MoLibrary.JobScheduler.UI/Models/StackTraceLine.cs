namespace MoLibrary.JobScheduler.UI.Models;

/// <summary>
/// 堆栈跟踪行的类型枚举
/// </summary>
public enum StackLineType
{
    /// <summary>
    /// 异常头部（异常类型和消息）
    /// </summary>
    ExceptionHeader,

    /// <summary>
    /// 堆栈帧（   at ...）
    /// </summary>
    StackFrame,

    /// <summary>
    /// 内部异常分隔符
    /// </summary>
    InnerException,

    /// <summary>
    /// 普通文本行
    /// </summary>
    PlainText,

    /// <summary>
    /// 解析错误
    /// </summary>
    ParseError
}

/// <summary>
/// 方法参数信息
/// </summary>
public class MethodParameter
{
    /// <summary>
    /// 参数类型（如 string, int, object 等）
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 参数名
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// 表示堆栈跟踪中的一行信息
/// </summary>
public class StackTraceLine
{
    /// <summary>
    /// 行的类型
    /// </summary>
    public StackLineType LineType { get; set; }

    /// <summary>
    /// 异常类型（如 System.InvalidOperationException）
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// 异常消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 完整的方法名（包括命名空间、类名、方法名）
    /// </summary>
    public string? FullMethod { get; set; }

    /// <summary>
    /// 命名空间和类名
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 类名
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// 方法名
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// 参数列表（包括括号）
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// 解析后的方法参数列表
    /// </summary>
    public List<MethodParameter> ParsedParameters { get; set; } = new();

    /// <summary>
    /// 文件路径（完整路径）
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 文件名（从路径中提取）
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 行号
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// 原始内容（用于降级显示或错误情况）
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    /// 内部异常是否展开（用于可折叠功能）
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// 所属的内部异常块索引（-1 表示不属于任何内部异常块）
    /// </summary>
    public int BelongToInnerExceptionIndex { get; set; } = -1;

    /// <summary>
    /// 错误消息（仅用于 ParseError 类型）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 堆栈跟踪解析结果
/// </summary>
public class ParseResult
{
    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 解析得到的行列表
    /// </summary>
    public List<StackTraceLine> Lines { get; set; } = new();

    /// <summary>
    /// 错误消息（仅当解析失败时有效）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 原始输入文本（用于降级显示）
    /// </summary>
    public string? OriginalText { get; set; }
}
