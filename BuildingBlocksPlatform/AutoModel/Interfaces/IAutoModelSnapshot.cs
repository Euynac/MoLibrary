using System.Collections;
using BuildingBlocksPlatform.AutoModel.Model;

namespace BuildingBlocksPlatform.AutoModel.Interfaces;

public interface IAutoModelSnapshotFactory
{
    /// <summary>
    /// 获取所有泛型AutoModel快照
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<AutoModelSnapshot> GetSnapshots();
}

/// <summary>
/// 泛型AutoModel快照接口
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAutoModelSnapshot<TModel>
{
    /// <summary>
    /// 获取所有字段支持的激活名
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<string> GetAllActivateNames();

    /// <summary>
    /// 根据传入字段激活名获取字段设置
    /// </summary>
    /// <param name="fieldActivateName"></param>
    /// <returns></returns>
    AutoField? GetField(string fieldActivateName);

    /// <summary>
    /// 获取所有字段设置
    /// </summary>
    /// <param name="fieldActivateNames"></param>
    /// <returns></returns>
    IReadOnlyList<AutoField> GetFields(IReadOnlyList<string>? fieldActivateNames = null);
}