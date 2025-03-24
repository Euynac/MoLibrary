using MoLibrary.Tool.Extensions;

namespace MoLibrary.DomainDrivenDesign.AutoController.Features;

public static class HttpMethodHelper
{
    public const string DefaultHttpVerb = "POST";

    public static Dictionary<string, string[]> ConventionalPrefixes { get; set; } = new()
    {
            {"GET", new[] {"GetList", "GetAll", "Get", "List"}},
            {"PUT", new[] {"Put", "Update"}},
            {"DELETE", new[] {"Delete", "Remove"}},
            {"POST", new[] {"Create", "Add", "Insert", "Post"}},
            {"PATCH", new[] {"Patch"}}
        };

    public static string GetConventionalVerbForMethodName(string methodName)
    {
        foreach (var conventionalPrefix in ConventionalPrefixes)
        {
            if (conventionalPrefix.Value.Any(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return conventionalPrefix.Key;
            }
        }

        return DefaultHttpVerb;
    }

    public static string RemoveHttpMethodPrefix(string methodName, string httpMethod)
    {
        var prefixes = ConventionalPrefixes.GetOrDefault(httpMethod);
        if (prefixes.IsNullOrEmptySet())
        {
            return methodName;
        }

        return methodName.RemovePreFix(prefixes!);
    }

    public static HttpMethod ConvertToHttpMethod(string? httpMethod)
    {
        switch (httpMethod?.ToUpperInvariant())
        {
            case "GET":
                return HttpMethod.Get;
            case "POST":
                return HttpMethod.Post;
            case "PUT":
                return HttpMethod.Put;
            case "DELETE":
                return HttpMethod.Delete;
            case "OPTIONS":
                return HttpMethod.Options;
            case "TRACE":
                return HttpMethod.Trace;
            case "HEAD":
                return HttpMethod.Head;
            case "PATCH":
                return new HttpMethod("PATCH");
            default:
                throw new Exception("Unknown HTTP METHOD: " + httpMethod);
        }
    }
}
