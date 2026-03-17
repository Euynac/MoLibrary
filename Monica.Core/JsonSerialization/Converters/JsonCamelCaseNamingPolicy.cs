using System.Text.Json;
using Monica.Tool.Extensions;

namespace Monica.Core.JsonSerialization.Converters;

public class JsonCamelCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.ToCamelCase();
}