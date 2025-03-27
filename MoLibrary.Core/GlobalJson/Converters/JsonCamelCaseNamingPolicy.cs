using MoLibrary.Tool.Extensions;
using System.Text.Json;

namespace MoLibrary.Core.GlobalJson.Converters;

public class JsonCamelCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.ToCamelCase(handleAbbreviations: true);
}