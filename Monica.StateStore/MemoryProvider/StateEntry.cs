namespace Monica.StateStore.MemoryProvider;

/// <summary>
/// 内存状态存储条目的非泛型接口，用于类型无关的访问
/// </summary>
public interface IStateEntry
{
    /// <summary>
    /// 状态数据（以 object 形式访问）
    /// </summary>
    object? Value { get; }

    /// <summary>
    /// ETag
    /// </summary>
    string ETag { get; }

    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    DateTimeOffset UpdatedAt { get; }

    DateTimeOffset? ExpiresAt { get; }
}

/// <summary>
/// 内存状态存储条目
/// </summary>
/// <typeparam name="T">状态数据类型</typeparam>
public class StateEntry<T> : IStateEntry
{
    /// <summary>
    /// 状态数据
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// IStateEntry 显式接口实现，将泛型值作为 object 返回
    /// </summary>
    object? IStateEntry.Value => Value;
    
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

    public DateTimeOffset? ExpiresAt { get; set; }

    public StateEntry()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public StateEntry(T? value, int version = 0, DateTimeOffset? expiresAt = null) : this()
    {
        Value = value;
        Version = version;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Updates the value, version, and expiration metadata.
    /// </summary>
    /// <param name="newValue">Updated state value.</param>
    /// <param name="expiresAt">Absolute expiration time, if any.</param>
    public void Update(T? newValue, DateTimeOffset? expiresAt = null)
    {
        Value = newValue;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
    }
} 
