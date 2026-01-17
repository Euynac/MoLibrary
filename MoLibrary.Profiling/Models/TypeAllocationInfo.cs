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
        // 处理泛型类型名称
        var genericIndex = fullName.IndexOf('`');
        var baseName = genericIndex >= 0 ? fullName[..genericIndex] : fullName;

        var lastDot = baseName.LastIndexOf('.');
        var shortBase = lastDot >= 0 ? baseName[(lastDot + 1)..] : baseName;

        // 如果是泛型，添加泛型参数部分
        if (genericIndex >= 0)
        {
            var bracketStart = fullName.IndexOf('[', genericIndex);
            if (bracketStart >= 0)
            {
                return shortBase + fullName[bracketStart..];
            }
            return shortBase + fullName[genericIndex..];
        }

        return shortBase;
    }
}
