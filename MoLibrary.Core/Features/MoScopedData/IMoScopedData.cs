namespace MoLibrary.Core.Features.MoScopedData;

/// <summary>
/// 临时数据接口，用于在Scoped生命周期内临时存储和管理状态数据
/// </summary>
public interface IMoScopedData
{
    /// <summary>
    /// 数据字典，用于存储键值对数据
    /// </summary>
    public IDictionary<string, object?> DataDict { get; }
    
    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <param name="value">数据值</param>
    void SetData(string key, object? value = null);
    
    /// <summary>
    /// 获取数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">数据键</param>
    /// <returns>数据值，如果不存在则返回默认值</returns>
    T? GetData<T>(string key);
    
    /// <summary>
    /// 获取数据，如果不存在则返回指定的默认值
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">数据键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>数据值或默认值</returns>
    T GetData<T>(string key, T defaultValue);
    
    /// <summary>
    /// 检查是否存在指定的数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>如果存在返回true，否则返回false</returns>
    bool HasData(string key);
    
    /// <summary>
    /// 移除指定的数据
    /// </summary>
    /// <param name="key">数据键</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    bool RemoveData(string key);
    
    /// <summary>
    /// 清空所有数据
    /// </summary>
    void Clear();
}