namespace Monica.AI.Providers;

internal static class AIProviderNaming
{
    public static string BuildDisplayName(string providerType, string providerId)
    {
        var normalizedType = providerType.Trim();
        var normalizedProviderId = providerId.Trim();

        if (string.Equals(normalizedType, normalizedProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedType;
        }

        return $"{normalizedType} ({normalizedProviderId})";
    }
}
