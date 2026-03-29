using Monica.UI.UIStackTrace.Models;
using System.Text.RegularExpressions;

namespace Monica.UI.UIStackTrace.Services;

/// <summary>
/// .NET Stack Trace Resolution Service
/// </summary>
public partial class StackTraceParserService
{
    /// <summary>
    /// Parse .NET stack trace information
    /// </summary>
    /// <param name="stackTraceText">Stack trace text</param>
    /// <returns>Analysis results</returns>
    public ParseResult Parse(string? stackTraceText)
    {
        if (string.IsNullOrWhiteSpace(stackTraceText))
        {
            return new ParseResult
            {
                Success = false,
                ErrorMessage = "堆栈信息为空",
                OriginalText = stackTraceText
            };
        }

        try
        {
            var lines = stackTraceText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var result = new ParseResult { Success = true, OriginalText = stackTraceText };

            int i = 0;
            int currentInnerExceptionIndex = -1;

            while (i < lines.Length)
            {
                string line = lines[i];

                // Check if it started with an internal exception (---> ExceptionType: Message)
                if (line.TrimStart().StartsWith("--->"))
                {
                    // Increase the inner exception block index.
                    currentInnerExceptionIndex++;

                    var innerExceptionLine = new StackTraceLine
                    {
                        LineType = StackLineType.InnerException,
                        RawContent = "--- Inner Exception ---",
                        BelongToInnerExceptionIndex = -1  // The marker line itself does not belong to any block.
                    };
                    result.Lines.Add(innerExceptionLine);

                    // Continue to process the exception header of this line (remove the 4 characters --->)
                    string cleanLine = line.TrimStart().Substring(4).TrimStart();
                    var innerExceptionHeaderMatch = ExceptionHeaderPattern().Match(cleanLine);
                    if (innerExceptionHeaderMatch.Success && IsValidExceptionType(innerExceptionHeaderMatch.Groups["exceptionType"].Value))
                    {
                        result.Lines.Add(new StackTraceLine
                        {
                            LineType = StackLineType.ExceptionHeader,
                            ExceptionType = innerExceptionHeaderMatch.Groups["exceptionType"].Value,
                            Message = innerExceptionHeaderMatch.Groups["message"].Value,
                            RawContent = cleanLine,
                            BelongToInnerExceptionIndex = currentInnerExceptionIndex
                        });
                    }

                    i++;
                    continue;
                }

                // Check if it is an abnormal header
                var exceptionHeaderMatch = ExceptionHeaderPattern().Match(line);
                if (exceptionHeaderMatch.Success && IsValidExceptionType(exceptionHeaderMatch.Groups["exceptionType"].Value))
                {
                    result.Lines.Add(new StackTraceLine
                    {
                        LineType = StackLineType.ExceptionHeader,
                        ExceptionType = exceptionHeaderMatch.Groups["exceptionType"].Value,
                        Message = exceptionHeaderMatch.Groups["message"].Value,
                        RawContent = line,
                        BelongToInnerExceptionIndex = currentInnerExceptionIndex
                    });

                    i++;
                    continue;
                }

                // Check if it is a stack frame
                var stackFrameMatch = StackFramePattern().Match(line);
                if (stackFrameMatch.Success)
                {
                    var traceLine = ParseStackFrame(stackFrameMatch, line);
                    traceLine.BelongToInnerExceptionIndex = currentInnerExceptionIndex;
                    result.Lines.Add(traceLine);
                    i++;
                    continue;
                }

                // Check if it is an internal abnormal end tag
                if (InnerExceptionBoundaryPattern().IsMatch(line))
                {
                    result.Lines.Add(new StackTraceLine
                    {
                        LineType = StackLineType.PlainText,
                        RawContent = line,
                        BelongToInnerExceptionIndex = currentInnerExceptionIndex
                    });

                    // End the current inner exception block.
                    currentInnerExceptionIndex = -1;

                    i++;
                    continue;
                }

                // Other lines are treated as normal text
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Lines.Add(new StackTraceLine
                    {
                        LineType = StackLineType.PlainText,
                        RawContent = line,
                        BelongToInnerExceptionIndex = currentInnerExceptionIndex
                    });
                }

                i++;
            }

            if (result.Lines.Count == 0)
            {
                return new ParseResult
                {
                    Success = false,
                    ErrorMessage = "未能识别有效的堆栈信息格式",
                    OriginalText = stackTraceText
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            return new ParseResult
            {
                Success = false,
                ErrorMessage = $"解析堆栈信息时出错：{ex.Message}",
                OriginalText = stackTraceText
            };
        }
    }

    /// <summary>
    /// Parse a single stack frame
    /// </summary>
    private static StackTraceLine ParseStackFrame(Match match, string rawContent)
    {
        var filePath = match.Groups["file"].Value;
        var line = new StackTraceLine
        {
            LineType = StackLineType.StackFrame,
            FullMethod = match.Groups["method"].Value,
            MethodName = match.Groups["methodname"].Value,
            Parameters = match.Groups["params"].Value,
            FilePath = filePath,
            RawContent = rawContent
        };

        // Extract file name
        if (!string.IsNullOrEmpty(filePath))
        {
            line.FileName = Path.GetFileName(filePath);
        }

        // Try to extract the line number
        if (int.TryParse(match.Groups["line"].Value, out int lineNumber))
        {
            line.LineNumber = lineNumber;
        }

        // Parse method parameters
        if (!string.IsNullOrEmpty(line.Parameters))
        {
            line.ParsedParameters = ParseMethodParameters(line.Parameters);
        }

        // Extract namespace and class names
        string namespaceValue = match.Groups["namespace"].Value;
        if (!string.IsNullOrEmpty(namespaceValue))
        {
            // Separate namespaces and class names
            int lastDotIndex = namespaceValue.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                line.Namespace = namespaceValue.Substring(0, lastDotIndex);
                line.ClassName = namespaceValue.Substring(lastDotIndex + 1);
            }
            else
            {
                line.ClassName = namespaceValue;
            }
        }

        return line;
    }

    /// <summary>
    /// Parse method parameter string, extract parameter type and name
    /// </summary>
    private static List<MethodParameter> ParseMethodParameters(string parametersStr)
    {
        var result = new List<MethodParameter>();

        if (string.IsNullOrWhiteSpace(parametersStr))
            return result;

        // remove brackets
        var content = parametersStr.Trim();
        if (content.StartsWith("(") && content.EndsWith(")"))
        {
            content = content.Substring(1, content.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(content))
            return result;

        // Split parameters by comma (need to consider commas in generics)
        var parameters = SplitParameters(content);

        foreach (var param in parameters)
        {
            var trimmedParam = param.Trim();
            if (string.IsNullOrEmpty(trimmedParam))
                continue;

            // Separate parameter types and names
            var parts = SplitParameterTypeAndName(trimmedParam);
            result.Add(new MethodParameter
            {
                Type = parts.type,
                Name = parts.name
            });
        }

        return result;
    }

    /// <summary>
    /// Split argument string by comma, accounting for commas in generics
    /// </summary>
    private static List<string> SplitParameters(string content)
    {
        var result = new List<string>();
        var currentParam = new System.Text.StringBuilder();
        int angleDepth = 0;

        foreach (var c in content)
        {
            switch (c)
            {
                case '<':
                    angleDepth++;
                    currentParam.Append(c);
                    break;
                case '>':
                    angleDepth--;
                    currentParam.Append(c);
                    break;
                case ',' when angleDepth == 0:
                    result.Add(currentParam.ToString());
                    currentParam.Clear();
                    break;
                default:
                    currentParam.Append(c);
                    break;
            }
        }

        if (currentParam.Length > 0)
        {
            result.Add(currentParam.ToString());
        }

        return result;
    }

    /// <summary>
    /// Separate types and names from parameter strings
    /// </summary>
    private static (string type, string name) SplitParameterTypeAndName(string param)
    {
        var trimmed = param.Trim();

        // Handling ref/out/in modifiers
        if (trimmed.StartsWith("ref "))
            trimmed = trimmed.Substring(4);
        else if (trimmed.StartsWith("out "))
            trimmed = trimmed.Substring(4);
        else if (trimmed.StartsWith("in "))
            trimmed = trimmed.Substring(3);

        // Find the last space from right to left as the separator between type and name
        int lastSpaceIndex = trimmed.LastIndexOf(' ');

        if (lastSpaceIndex <= 0)
        {
            // No spaces found, the whole string is type (shouldn't happen)
            return (trimmed, string.Empty);
        }

        var type = trimmed.Substring(0, lastSpaceIndex).Trim();
        var name = trimmed.Substring(lastSpaceIndex + 1).Trim();

        return (type, name);
    }

    /// <summary>
    /// Check if it is a valid exception type
    /// </summary>
    private static bool IsValidExceptionType(string exceptionType)
    {
        if (string.IsNullOrWhiteSpace(exceptionType))
            return false;

        // Contains "Exception" or "Error", or the standard namespace format
        if (exceptionType.Contains("Exception") || exceptionType.Contains("Error"))
            return true;

        // Check common exception type prefixes
        var validPrefixes = new[] { "System.", "Microsoft.", "Monica.", "Namespace.", "ApplicationException" };
        return validPrefixes.Any(prefix => exceptionType.StartsWith(prefix)) ||
               // Also accept any format containing dots (usually namespace.classname).
               exceptionType.Contains(".");
    }

    /// <summary>
    /// Checks if the given text looks like a stack trace
    /// </summary>
    public bool IsLikelyStackTrace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check for common stack trace characteristics
        return text.Contains("\n   at ") ||
               text.Contains("Exception:") ||
               text.Contains("StackTrace:") ||
               ExceptionHeaderPattern().IsMatch(text);
    }

    [GeneratedRegex(@"^(?<exceptionType>[\w\.]+(?:\[\w+\])?)\s*:\s*(?<message>.*)$", RegexOptions.Compiled)]
    private static partial Regex ExceptionHeaderPattern();

    [GeneratedRegex(
        @"^\s+at\s+(?<method>(?<namespace>[\w\.<>]+)\.(?<methodname>[\w<>]+))\s*(?<params>\([^\)]*\))?\s*(?:in\s+(?<file>.+?)\s*:line\s+(?<line>\d+))?",
        RegexOptions.Compiled)]
    private static partial Regex StackFramePattern();

    [GeneratedRegex(
        @"^\s*---+\s*(?:End of\s+)?(?:Inner\s+)?[Ee]xception(?:\s+stack trace)?\s*---+",
        RegexOptions.Compiled)]
    private static partial Regex InnerExceptionBoundaryPattern();
}
