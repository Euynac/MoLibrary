namespace MoLibrary.StateStore.MemoryProvider;

/// <summary>
/// 内存状态存储条目
/// </summary>
/// <typeparam name="T">状态数据类型</typeparam>
public class StateEntry<T>
{
    /// <summary>
    /// 状态数据
    /// </summary>
    public T? Value { get; set; }
    
    /// <summary>
    /// 版本号，从0开始，每次更新递增
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// ETag，基于版本号生成
    /// </summary>
    public string ETag => Version.ToString();
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public StateEntry()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public StateEntry(T? value, int version = 0) : this()
    {
        Value = value;
        Version = version;
    }

    /// <summary>
    /// 更新状态数据并递增版本号
    /// </summary>
    /// <param name="newValue">新的状态数据</param>
    public void Update(T? newValue)
    {
        Value = newValue;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
} 