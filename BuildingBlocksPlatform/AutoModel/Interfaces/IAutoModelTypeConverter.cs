using BuildingBlocksPlatform.AutoModel.Configurations;
using BuildingBlocksPlatform.AutoModel.Exceptions;
using BuildingBlocksPlatform.AutoModel.Model;

namespace BuildingBlocksPlatform.AutoModel.Interfaces;

/// <summary>
/// 自动模型类型转换器
/// </summary>
public interface IAutoModelTypeConverter
{
    /// <summary>
    /// 转换字段值
    /// </summary>
    /// <param name="value"></param>
    /// <param name="typeSetting"></param>
    /// <param name="features"></param>
    /// <exception cref="AutoModelValueConvertException"></exception>
    /// <returns></returns>
    dynamic? ConvertEntrance(string value, AutoFieldTypeSetting typeSetting, EFieldConditionFeatures features);
}