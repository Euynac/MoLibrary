namespace Monica.UI.UIStackTrace.Models;

/// <summary>
/// Type enum for stack trace lines
/// </summary>
public enum StackLineType
{
    /// <summary>
    /// Exception header (exception type and message)
    /// </summary>
    ExceptionHeader,

    /// <summary>
    /// stack frame (at...)
    /// </summary>
    StackFrame,

    /// <summary>
    /// Internal exception separator
    /// </summary>
    InnerException,

    /// <summary>
    /// plain text line
    /// </summary>
    PlainText,

    /// <summary>
    /// Parse error
    /// </summary>
    ParseError
}

/// <summary>
/// Method parameter information
/// </summary>
public class MethodParameter
{
    /// <summary>
    /// Parameter type (such as string, int, object, etc.)
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Parameter name
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Represents a line of information in the stack trace
/// </summary>
public class StackTraceLine
{
    /// <summary>
    /// row type
    /// </summary>
    public StackLineType LineType { get; set; }

    /// <summary>
    /// Exception type (such as System.InvalidOperationException)
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Exception message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Complete method name (including namespace, class name, method name)
    /// </summary>
    public string? FullMethod { get; set; }

    /// <summary>
    /// Namespaces and class names
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Class name
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// method name
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// Parameter list (including parentheses)
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Parsed method parameter list
    /// </summary>
    public List<MethodParameter> ParsedParameters { get; set; } = new();

    /// <summary>
    /// File path (full path)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Filename (extracted from path)
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Line number
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Original content (for degraded display or error conditions)
    /// </summary>
    public string? RawContent { get; set; }

    /// <summary>
    /// Whether the inner exception is expanded (for collapsible functions)
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Index of the inner exception block it belongs to (-1 means it does not belong to any inner exception block)
    /// </summary>
    public int BelongToInnerExceptionIndex { get; set; } = -1;

    /// <summary>
    /// Error message (only for ParseError type)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Stack trace parsing results
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Is parsing successful?
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// parsed line list
    /// </summary>
    public List<StackTraceLine> Lines { get; set; } = new();

    /// <summary>
    /// Error message (only valid if parsing fails)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Original input text (for degraded display)
    /// </summary>
    public string? OriginalText { get; set; }
}
