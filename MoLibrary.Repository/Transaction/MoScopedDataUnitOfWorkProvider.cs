using MoLibrary.Core.Features.MoScopedData;

namespace MoLibrary.Repository.Transaction;

/// <summary>
/// 环境数据默认实现类，用于在Scoped生命周期内临时存储和管理状态数据。
/// </summary>
public class MoScopedDataUnitOfWorkProvider(IMoUnitOfWorkManager manager) : IMoScopedData
{
    /// <summary>
    /// 数据字典，用于存储键值对数据
    /// </summary>
    public IDictionary<string, object?> DataDict => manager.Current?.Items ?? new Dictionary<string, object?>();

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <param name="value">数据值</param>
    public void SetData(string key, object? value = null)
    {
        var current = manager.Current;
        if (current != null)
        {
            current.Items[key] = value;
        }
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">数据键</param>
    /// <returns>数据值，如果不存在则返回默认值</returns>
    public T? GetData<T>(string key)
    {
        var current = manager.Current;
        if (current?.Items.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// 获取数据，如果不存在则返回指定的默认值
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">数据键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>数据值或默认值</returns>
    public T GetData<T>(string key, T defaultValue)
    {
        var current = manager.Current;
        if (current?.Items.TryGetValue(key, out var value) == true && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// 检查是否存在指定的数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>如果存在返回true，否则返回false</returns>
    public bool HasData(string key)
    {
        var current = manager.Current;
        return current?.Items.ContainsKey(key) == true;
    }

    /// <summary>
    /// 移除指定的数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    public bool RemoveData(string key)
    {
        var current = manager.Current;
        if (current != null)
        {
            return current.Items.Remove(key);
        }
        return false;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        var current = manager.Current;
        current?.Items.Clear();
    }
}