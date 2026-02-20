using System.Text.Json.Serialization;

namespace Monica.Localization.Json;

internal record JsonLocalizationFile(
    [property: JsonPropertyName("texts")]
    Dictionary<string, object> Texts);
