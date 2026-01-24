using MoLibrary.AI.Models;

namespace MoLibrary.AI.Services;

/// <summary>
/// AI 模型信息目录
/// </summary>
public class AIModelCatalog
{
    private readonly Dictionary<EAIProviderType, List<AIModelInfo>> _models = new();

    /// <summary>
    /// 添加模型信息
    /// </summary>
    public void AddModel(EAIProviderType providerType, AIModelInfo model)
    {
        if (!_models.TryGetValue(providerType, out var list))
        {
            list = new List<AIModelInfo>();
            _models[providerType] = list;
        }

        var existingIndex = list.FindIndex(m => string.Equals(m.ModelName, model.ModelName, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            list[existingIndex] = model;
            return;
        }

        list.Add(model);
    }

    /// <summary>
    /// 批量添加模型信息
    /// </summary>
    public void AddModels(EAIProviderType providerType, IEnumerable<AIModelInfo> models)
    {
        foreach (var model in models)
        {
            AddModel(providerType, model);
        }
    }

    /// <summary>
    /// 获取指定 Provider 的模型列表
    /// </summary>
    public IReadOnlyList<AIModelInfo> GetModels(EAIProviderType providerType)
    {
        if (_models.TryGetValue(providerType, out var list))
        {
            return list.AsReadOnly();
        }

        return Array.Empty<AIModelInfo>();
    }

    /// <summary>
    /// 获取指定 Provider 的模型名称列表
    /// </summary>
    public IReadOnlyList<string> GetModelNames(EAIProviderType providerType)
    {
        return GetModels(providerType).Select(m => m.ModelName).ToList();
    }

    /// <summary>
    /// 获取指定模型信息
    /// </summary>
    public AIModelInfo? GetModel(EAIProviderType providerType, string modelName)
    {
        return GetModels(providerType).FirstOrDefault(m =>
            string.Equals(m.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
    }
}
