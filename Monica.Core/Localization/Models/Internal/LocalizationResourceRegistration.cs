using System.Reflection;

namespace Monica.Core.Localization.Models.Internal;

internal sealed record LocalizationResourceRegistration(
    Type ResourceType,
    Assembly Assembly,
    string BasePath)
{
    public static LocalizationResourceRegistration Create(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        var basePath = string.IsNullOrWhiteSpace(resourceType.Namespace)
            ? resourceType.Name
            : $"{resourceType.Namespace}.{resourceType.Name}";

        return new LocalizationResourceRegistration(resourceType, resourceType.Assembly, basePath);
    }
}
