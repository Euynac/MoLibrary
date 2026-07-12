using Monica.AI.Models;

namespace Monica.AI.Services;

/// <summary>
/// Global AI model information catalog
/// </summary>
internal sealed class AIModelCatalog
{
    private readonly Dictionary<string, AIModelInfo> _models = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Add a model to the catalog. If a model with the same name already exists, it is replaced.
    /// </summary>
    public void AddModel(AIModelInfo model)
    {
        _models[model.ModelName] = model;
    }

    /// <summary>
    /// Add multiple models to the catalog
    /// </summary>
    public void AddModels(IEnumerable<AIModelInfo> models)
    {
        foreach (var model in models)
        {
            AddModel(model);
        }
    }

    /// <summary>
    /// Get all models in the catalog
    /// </summary>
    public IReadOnlyList<AIModelInfo> GetModels()
    {
        return _models.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get all model names in the catalog
    /// </summary>
    public IReadOnlyList<string> GetModelNames()
    {
        return _models.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get a specific model by name (case-insensitive)
    /// </summary>
    public AIModelInfo? GetModel(string modelName)
    {
        return _models.GetValueOrDefault(modelName);
    }
}
