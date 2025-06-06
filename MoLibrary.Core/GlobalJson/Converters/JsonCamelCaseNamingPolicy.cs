using System.Text.Json;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.GlobalJson.Converters;

public class JsonCamelCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.ToCamelCase();
}