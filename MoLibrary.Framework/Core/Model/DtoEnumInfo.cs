namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// 枚举值信息Dto
/// </summary>
public class DtoEnumValue
{
    /// <summary>
    /// 索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 枚举信息Dto
/// </summary>
public class DtoEnumInfo
{
    /// <summary>
    /// 枚举名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 枚举值列表
    /// </summary>
    public List<DtoEnumValue> Values { get; set; } = [];
}

/// <summary>
/// 程序集枚举信息Dto
/// </summary>
public class DtoAssemblyEnumInfo
{
    /// <summary>
    /// 程序集来源
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// 枚举列表
    /// </summary>
    public List<DtoEnumInfo> Enums { get; set; } = [];
}