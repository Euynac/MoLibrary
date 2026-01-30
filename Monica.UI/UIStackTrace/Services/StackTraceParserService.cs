using Monica.UI.UIStackTrace.Models;
using System.Text.RegularExpressions;

namespace Monica.UI.UIStackTrace.Services;

/// <summary>
/// .NET 堆栈跟踪解析服务
/// </summary>
public class StackTraceParserService
{
    /// <summary>
    /// 异常头部正则表达式（ExceptionType: Message）
    /// </summary>
    private static readonly Regex ExceptionHeaderRegex = new(
        @"^(?<exceptionType>[\w\.]+(?:\[\w+\])?)\s*:\s*(?<message>.*)$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// 堆栈帧正则表达式（   at Namespace.Class.Method(Params) in File.cs:line 123）
    /// </summary>
    private static readonly Regex StackFrameRegex = new(
        @"^\s+at\s+(?<method>(?<namespace>[\w\.<>]+)\.(?<methodname>[\w<>]+))\s*(?<params>\([^\)]*\))?\s*(?:in\s+(?<file>.+?)\s*:line\s+(?<line>\d+))?",
        RegexOptions.Compiled
    );

    /// <summary>
    /// 内部异常结束标记正则表达式
    /// </summary>
    private static readonly Regex InnerExceptionEndRegex = new(
        @"^\s*---+\s*(?:End of\s+)?(?:Inner\s+)?[Ee]xception(?:\s+stack trace)?\s*---+",
        RegexOptions.Compiled
    );

    /// <summary>
    /// 解析 .NET 堆栈跟踪信息
    /// </summary>
    /// <param name="stackTraceText">堆栈跟踪文本</param>
    /// <returns>解析结果</returns>
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
            var lines = stackTraceText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new ParseResult { Success = true, OriginalText = stackTraceText };

            int i = 0;
            int currentInnerExceptionIndex = -1;

            while (i < lines.Length)
            {
                string line = lines[i];

                // 检查是否为内部异常开始（---> ExceptionType: Message）
                if (line.TrimStart().StartsWith("--->"))
                {
                    // 增加内部异常块索引
                    currentInnerExceptionIndex++;

                    var innerExceptionLine = new StackTraceLine
                    {
                        LineType = StackLineType.InnerException,
                        RawContent = "--- Inner Exception ---",
                        BelongToInnerExceptionIndex = -1  // 标记行本身不属于任何块
                    };
                    result.Lines.Add(innerExceptionLine);

                    // 继续处理该行的异常头部（去掉 ---> 这4个字符）
                    string cleanLine = line.TrimStart().Substring(4).TrimStart();
                    var exceptionMatch = ExceptionHeaderRegex.Match(cleanLine);
                    if (exceptionMatch.Success && IsValidExceptionType(exceptionMatch.Groups["exceptionType"].Value))
                    {
                        result.Lines.Add(new StackTraceLine
                        {
                            LineType = StackLineType.ExceptionHeader,
                            ExceptionType = exceptionMatch.Groups["exceptionType"].Value,
                            Message = exceptionMatch.Groups["message"].Value,
                            RawContent = cleanLine,
                            BelongToInnerExceptionIndex = currentInnerExceptionIndex
                        });
                    }

                    i++;
                    continue;
                }

                // 检查是否为异常头部
                var exceptionMatch2 = ExceptionHeaderRegex.Match(line);
                if (exceptionMatch2.Success && IsValidExceptionType(exceptionMatch2.Groups["exceptionType"].Value))
                {
                    result.Lines.Add(new StackTraceLine
                    {
                        LineType = StackLineType.ExceptionHeader,
                        ExceptionType = exceptionMatch2.Groups["exceptionType"].Value,
                        Message = exceptionMatch2.Groups["message"].Value,
                        RawContent = line,
                        BelongToInnerExceptionIndex = currentInnerExceptionIndex
                    });

                    i++;
                    continue;
                }

                // 检查是否为堆栈帧
                var frameMatch = StackFrameRegex.Match(line);
                if (frameMatch.Success)
                {
                    var traceLine = ParseStackFrame(frameMatch, line);
                    traceLine.BelongToInnerExceptionIndex = currentInnerExceptionIndex;
                    result.Lines.Add(traceLine);
                    i++;
                    continue;
                }

                // 检查是否为内部异常结束标记
                if (InnerExceptionEndRegex.IsMatch(line))
                {
                    result.Lines.Add(new StackTraceLine
                    {
                        LineType = StackLineType.PlainText,
                        RawContent = line,
                        BelongToInnerExceptionIndex = currentInnerExceptionIndex
                    });

                    // 结束当前内部异常块
                    currentInnerExceptionIndex = -1;

                    i++;
                    continue;
                }

                // 其他行视为普通文本
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
    /// 解析单个堆栈帧
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

        // 提取文件名
        if (!string.IsNullOrEmpty(filePath))
        {
            line.FileName = System.IO.Path.GetFileName(filePath);
        }

        // 尝试提取行号
        if (int.TryParse(match.Groups["line"].Value, out int lineNumber))
        {
            line.LineNumber = lineNumber;
        }

        // 解析方法参数
        if (!string.IsNullOrEmpty(line.Parameters))
        {
            line.ParsedParameters = ParseMethodParameters(line.Parameters);
        }

        // 提取命名空间和类名
        string namespaceValue = match.Groups["namespace"].Value;
        if (!string.IsNullOrEmpty(namespaceValue))
        {
            // 分离命名空间和类名
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
    /// 解析方法参数字符串，提取参数类型和名称
    /// </summary>
    private static List<MethodParameter> ParseMethodParameters(string parametersStr)
    {
        var result = new List<MethodParameter>();

        if (string.IsNullOrWhiteSpace(parametersStr))
            return result;

        // 移除括号
        var content = parametersStr.Trim();
        if (content.StartsWith("(") && content.EndsWith(")"))
        {
            content = content.Substring(1, content.Length - 2);
        }

        if (string.IsNullOrWhiteSpace(content))
            return result;

        // 按逗号分割参数（需要考虑泛型中的逗号）
        var parameters = SplitParameters(content);

        foreach (var param in parameters)
        {
            var trimmedParam = param.Trim();
            if (string.IsNullOrEmpty(trimmedParam))
                continue;

            // 分离参数类型和名称
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
    /// 按逗号分割参数字符串，考虑泛型中的逗号
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
    /// 从参数字符串分离类型和名称
    /// </summary>
    private static (string type, string name) SplitParameterTypeAndName(string param)
    {
        var trimmed = param.Trim();

        // 处理 ref/out/in 修饰符
        if (trimmed.StartsWith("ref "))
            trimmed = trimmed.Substring(4);
        else if (trimmed.StartsWith("out "))
            trimmed = trimmed.Substring(4);
        else if (trimmed.StartsWith("in "))
            trimmed = trimmed.Substring(3);

        // 从右到左找最后一个空格，作为类型和名称的分隔符
        int lastSpaceIndex = trimmed.LastIndexOf(' ');

        if (lastSpaceIndex <= 0)
        {
            // 没有找到空格，整个字符串是类型（不应该发生）
            return (trimmed, string.Empty);
        }

        var type = trimmed.Substring(0, lastSpaceIndex).Trim();
        var name = trimmed.Substring(lastSpaceIndex + 1).Trim();

        return (type, name);
    }

    /// <summary>
    /// 检查是否为有效的异常类型
    /// </summary>
    private static bool IsValidExceptionType(string exceptionType)
    {
        if (string.IsNullOrWhiteSpace(exceptionType))
            return false;

        // 包含 "Exception" 或 "Error"，或者是标准的命名空间格式
        if (exceptionType.Contains("Exception") || exceptionType.Contains("Error"))
            return true;

        // 检查常见的异常类型前缀
        var validPrefixes = new[] { "System.", "Microsoft.", "Monica.", "Namespace.", "ApplicationException" };
        return validPrefixes.Any(prefix => exceptionType.StartsWith(prefix)) ||
               // 也接受任何包含点的格式（通常是命名空间.类名）
               exceptionType.Contains(".");
    }

    /// <summary>
    /// 检查给定的文本是否看起来像堆栈跟踪
    /// </summary>
    public bool IsLikelyStackTrace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // 检查是否包含常见的堆栈跟踪特征
        return text.Contains("\n   at ") ||
               text.Contains("Exception:") ||
               text.Contains("StackTrace:") ||
               ExceptionHeaderRegex.IsMatch(text);
    }
}
