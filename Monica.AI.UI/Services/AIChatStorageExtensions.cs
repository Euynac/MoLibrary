using Monica.UI.Services;

namespace Monica.AI.UI.Services;

/// <summary>
/// Browser storage extension methods for AI chat preferences
/// </summary>
public static class AIChatStorageExtensions
{
    private const string AIChatCategory = "ai-chat";

    /// <summary>
    /// Get the default provider ID from storage
    /// </summary>
    public static Task<string?> GetDefaultProviderAsync(this IMoBrowserStorage storage)
    {
        return storage.GetAsync<string?>($"{AIChatCategory}:default-provider", null);
    }

    /// <summary>
    /// Save the default provider ID to storage
    /// </summary>
    public static Task SaveDefaultProviderAsync(this IMoBrowserStorage storage, string providerId)
    {
        return storage.SetAsync($"{AIChatCategory}:default-provider", providerId);
    }

    /// <summary>
    /// Get the default model name from storage
    /// </summary>
    public static Task<string?> GetDefaultModelAsync(this IMoBrowserStorage storage)
    {
        return storage.GetAsync<string?>($"{AIChatCategory}:default-model", null);
    }

    /// <summary>
    /// Save the default model name to storage
    /// </summary>
    public static Task SaveDefaultModelAsync(this IMoBrowserStorage storage, string modelName)
    {
        return storage.SetAsync($"{AIChatCategory}:default-model", modelName);
    }
}
