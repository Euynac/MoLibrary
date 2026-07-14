using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Monica.WebApi.AutoControllers.Abstractions;
using Monica.WebApi.AutoControllers.Models;

namespace Monica.WebApi.AutoControllers.Services.Support;

internal sealed class ConventionalHttpMethodResolver(IOptions<CrudControllerOption> options)
    : IConventionalHttpMethodResolver
{
    private readonly ConventionSnapshot _snapshot = CreateSnapshot(options.Value.HttpMethods);

    public string Resolve(string actionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);

        var match = _snapshot.PrefixesByPrecedence.FirstOrDefault(item =>
            actionName.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase));

        return match?.HttpMethod ?? _snapshot.DefaultHttpMethod;
    }

    public string RemovePrefix(string actionName, string httpMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);

        if (!_snapshot.PrefixesByHttpMethod.TryGetValue(httpMethod, out var prefixes))
        {
            return actionName;
        }

        var prefix = prefixes.FirstOrDefault(candidate =>
            actionName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

        return prefix is null ? actionName : actionName[prefix.Length..];
    }

    private static ConventionSnapshot CreateSnapshot(ConventionalHttpMethodOption option)
    {
        var prefixesByHttpMethod = CreatePrefixMap(option.Prefixes);
        var prefixesByPrecedence = prefixesByHttpMethod
            .SelectMany(pair => pair.Value.Select(prefix => new HttpMethodPrefix(pair.Key, prefix)))
            .OrderByDescending(item => item.Prefix.Length)
            .ThenBy(item => item.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        return new ConventionSnapshot(
            NormalizeHttpMethod(option.DefaultHttpMethod),
            prefixesByHttpMethod,
            prefixesByPrecedence);
    }

    private static ImmutableDictionary<string, ImmutableArray<string>> CreatePrefixMap(
        IDictionary<string, string[]> configuredPrefixes)
    {
        var prefixOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (configuredHttpMethod, configuredMethodPrefixes) in configuredPrefixes)
        {
            var httpMethod = NormalizeHttpMethod(configuredHttpMethod);
            var prefixes = (configuredMethodPrefixes ?? [])
                .Select(prefix => prefix?.Trim())
                .Where(prefix => !string.IsNullOrEmpty(prefix))
                .Select(prefix => prefix!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(prefix => prefix.Length)
                .ThenBy(prefix => prefix, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            foreach (var prefix in prefixes)
            {
                if (prefixOwners.TryGetValue(prefix, out var owner) &&
                    !owner.Equals(httpMethod, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The AutoController action prefix '{prefix}' is assigned to both '{owner}' and '{httpMethod}'.");
                }

                prefixOwners[prefix] = httpMethod;
            }

            builder[httpMethod] = prefixes;
        }

        return builder.ToImmutable();
    }

    private static string NormalizeHttpMethod(string httpMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(httpMethod);
        return new HttpMethod(httpMethod.Trim()).Method.ToUpperInvariant();
    }

    private sealed record ConventionSnapshot(
        string DefaultHttpMethod,
        ImmutableDictionary<string, ImmutableArray<string>> PrefixesByHttpMethod,
        ImmutableArray<HttpMethodPrefix> PrefixesByPrecedence);

    private sealed record HttpMethodPrefix(string HttpMethod, string Prefix);
}
