namespace MoLibrary.Profiling.Models;

/// <summary>
///     类型分配信息 - 特定类型的内存分配统计
/// </summary>
public class TypeAllocationInfo
{
    private long _allocationCount;
    private long _totalBytes;

    /// <summary>
    ///     完整类型名称 (如 "System.String", "MyNamespace.MyClass")
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    ///     简短类型名称 (如 "String", "MyClass")
    /// </summary>
    public string ShortTypeName => GetShortTypeName(TypeName);

    /// <summary>
    ///     分配实例数量
    /// </summary>
    public long AllocationCount
    {
        get => Interlocked.Read(ref _allocationCount);
        set => Interlocked.Exchange(ref _allocationCount, value);
    }

    /// <summary>
    ///     总分配字节数
    /// </summary>
    public long TotalBytes
    {
        get => Interlocked.Read(ref _totalBytes);
        set => Interlocked.Exchange(ref _totalBytes, value);
    }

    /// <summary>
    ///     原子性地增加分配数量
    /// </summary>
    public void AddAllocationCount(long value) => Interlocked.Add(ref _allocationCount, value);

    /// <summary>
    ///     原子性地增加字节数
    /// </summary>
    public void AddTotalBytes(long value) => Interlocked.Add(ref _totalBytes, value);

    /// <summary>
    ///     平均分配大小 (字节)
    /// </summary>
    public double AverageSize => AllocationCount > 0 ? (double)TotalBytes / AllocationCount : 0;

    /// <summary>
    ///     占总分配数的百分比
    /// </summary>
    public double AllocationPercentage { get; set; }

    /// <summary>
    ///     占总字节数的百分比
    /// </summary>
    public double BytesPercentage { get; set; }

    /// <summary>
    ///     是否为大对象 (>85KB, 分配在 LOH 上)
    /// </summary>
    public bool IsLargeObjectHeap => AverageSize >= 85000;

    /// <summary>
    ///     最后一次看到分配的时间 (UTC)
    /// </summary>
    public DateTime LastSeenUtc { get; set; }

    private static string GetShortTypeName(string fullName)
    {
        // Handle C#-style generic types (e.g., HashSet<Namespace.Type>)
        // But NOT compiler-generated types that start with < (e.g., <GetEvents>d__19)
        var angleBracketIndex = fullName.IndexOf('<');
        if (angleBracketIndex > 0) // > 0, not >= 0, to exclude compiler-generated types
        {
            // Verify it's a real generic by checking for matching closing bracket
            var closingIndex = FindMatchingClosingBracket(fullName, angleBracketIndex, '<', '>');
            if (closingIndex == fullName.Length - 1)
            {
                // Get base type name before '<'
                var basePart = fullName[..angleBracketIndex];
                var lastDot = basePart.LastIndexOf('.');
                var shortBase = lastDot >= 0 ? basePart[(lastDot + 1)..] : basePart;

                // Process generic arguments recursively
                var genericArgs = fullName[(angleBracketIndex + 1)..^1]; // Content inside <>
                var shortArgs = ShortenGenericArguments(genericArgs);

                return $"{shortBase}<{shortArgs}>";
            }
        }

        // Handle CLR-style generic types with backtick (e.g., Dictionary`2[[...]])
        var genericIndex = fullName.IndexOf('`');
        if (genericIndex >= 0)
        {
            var baseName = fullName[..genericIndex];
            var lastDot = baseName.LastIndexOf('.');
            var shortBase = lastDot >= 0 ? baseName[(lastDot + 1)..] : baseName;

            var bracketStart = fullName.IndexOf('[', genericIndex);
            if (bracketStart >= 0)
            {
                return shortBase + fullName[bracketStart..];
            }
            return shortBase + fullName[genericIndex..];
        }

        // Handle square bracket generics without backtick (e.g., Tables[System.IntPtr,SkiaSharp.SKObject])
        var squareBracketIndex = fullName.IndexOf('[');
        if (squareBracketIndex > 0 && fullName.EndsWith(']'))
        {
            var basePart = fullName[..squareBracketIndex];
            var lastDot = basePart.LastIndexOf('.');
            var shortBase = lastDot >= 0 ? basePart[(lastDot + 1)..] : basePart;

            var argsContent = fullName[(squareBracketIndex + 1)..^1]; // Content inside []
            var shortArgs = ShortenSquareBracketArguments(argsContent);

            return $"{shortBase}[{shortArgs}]";
        }

        // Handle non-generic types
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }

    private static int FindMatchingClosingBracket(string text, int openIndex, char open, char close)
    {
        var depth = 1;
        for (var i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static string ShortenGenericArguments(string args)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < args.Length; i++)
        {
            var c = args[i];
            if (c == '<' || c == '[') depth++;
            else if (c == '>' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(GetShortTypeName(args[start..i].Trim()));
                start = i + 1;
            }
        }

        // Add the last argument
        if (start < args.Length)
        {
            result.Add(GetShortTypeName(args[start..].Trim()));
        }

        return string.Join(", ", result);
    }

    private static string ShortenSquareBracketArguments(string args)
    {
        // Handle nested CLR format: [[Type1],[Type2]] or [[Type1, Assembly],[Type2, Assembly]]
        if (args.StartsWith('['))
        {
            var result = new List<string>();
            var depth = 0;
            var start = -1;

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == '[')
                {
                    if (depth == 0) start = i + 1;
                    depth++;
                }
                else if (args[i] == ']')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        var typeName = args[start..i];
                        // Remove assembly info if present (after comma not inside brackets)
                        var commaDepth = 0;
                        for (var j = 0; j < typeName.Length; j++)
                        {
                            if (typeName[j] == '[') commaDepth++;
                            else if (typeName[j] == ']') commaDepth--;
                            else if (typeName[j] == ',' && commaDepth == 0)
                            {
                                typeName = typeName[..j].Trim();
                                break;
                            }
                        }
                        result.Add(GetShortTypeName(typeName));
                        start = -1;
                    }
                }
            }

            return string.Join(", ", result);
        }

        // Simple comma-separated format: Type1,Type2
        return ShortenGenericArguments(args);
    }
}
