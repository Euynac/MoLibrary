using System.Reflection;

namespace Monica.Localization.Models;

internal record LocalizationResourceRegistration(
    Type ResourceType,
    Assembly Assembly,
    string BasePath);
