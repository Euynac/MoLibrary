namespace Monica.Core.Module.Models;

/// <summary>
/// 模块唯一标识符。支持从内置枚举 EMoModuleKey 或字符串创建。
/// </summary>
public readonly record struct ModuleKey : IEquatable<ModuleKey>
{
    /// <summary>
    /// 模块键的字符串值
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 是否为 Monica 内置模块
    /// </summary>
    public bool IsBuiltIn { get; }

    private ModuleKey(string value, bool isBuiltIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
        IsBuiltIn = isBuiltIn;
    }

    /// <summary>
    /// 创建第三方模块键，建议格式：Vendor.ModuleName
    /// </summary>
    public static ModuleKey Create(string key) => new(key, isBuiltIn: false);

    /// <summary>
    /// 从内置枚举隐式转换（内置模块使用）
    /// </summary>
    public static implicit operator ModuleKey(EMoModuleKey moduleKey) =>
        new(moduleKey.ToString(), isBuiltIn: true);

    /// <summary>
    /// 从字符串隐式转换（第三方模块使用）
    /// </summary>
    public static implicit operator ModuleKey(string key) => Create(key);

    /// <summary>
    /// 隐式转换为字符串
    /// </summary>
    public static implicit operator string(ModuleKey key) => key.Value;

    public override string ToString() => Value;
}
