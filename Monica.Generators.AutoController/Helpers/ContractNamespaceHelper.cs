using System;
using System.Collections.Generic;
using System.Linq;

namespace Monica.Generators.AutoController.Helpers;

/// <summary>
/// Resolves the root contract namespace used by generated RPC client types.
/// </summary>
internal static class ContractNamespaceHelper
{
    private static readonly HashSet<string> KnownContractLeafSegments = new(StringComparer.Ordinal)
    {
        "Requests",
        "Models",
        "Events",
        "AppInterfaces",
        "Implements"
    };

    /// <summary>
    /// Resolves the contract namespace root from related request/response namespaces.
    /// </summary>
    public static string? ResolveContractNamespaceRoot(IEnumerable<string> relatedNamespaces, string? domainName)
    {
        var namespaceList = relatedNamespaces
            .Where(static ns => !string.IsNullOrWhiteSpace(ns))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (namespaceList.Count == 0)
        {
            return null;
        }

        var domainBasedRoot = ResolveFromDomainSegment(namespaceList, domainName);
        if (!string.IsNullOrWhiteSpace(domainBasedRoot))
        {
            return domainBasedRoot;
        }

        var normalizedRoots = namespaceList
            .Select(TrimKnownContractLeafSegments)
            .Where(static ns => !string.IsNullOrWhiteSpace(ns))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedRoots.Count == 1)
        {
            return normalizedRoots[0];
        }

        return null;
    }

    private static string? ResolveFromDomainSegment(IEnumerable<string> relatedNamespaces, string? domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return null;
        }

        var domainSegment = $"Domain{domainName}";
        var roots = relatedNamespaces
            .Select(ns => TakeNamespaceThroughSegment(ns, domainSegment))
            .Where(static ns => !string.IsNullOrWhiteSpace(ns))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return roots.Count switch
        {
            0 => null,
            1 => roots[0],
            _ => null
        };
    }

    private static string? TakeNamespaceThroughSegment(string namespaceValue, string segmentName)
    {
        var segments = namespaceValue.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
        var segmentIndex = Array.IndexOf(segments, segmentName);
        if (segmentIndex < 0)
        {
            return null;
        }

        return string.Join(".", segments.Take(segmentIndex + 1));
    }

    private static string? TrimKnownContractLeafSegments(string namespaceValue)
    {
        var segments = namespaceValue
            .Split(['.'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count == 0)
        {
            return null;
        }

        while (segments.Count > 0 && !KnownContractLeafSegments.Contains(segments[segments.Count - 1]))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count == 0)
        {
            return null;
        }

        segments.RemoveAt(segments.Count - 1);
        return segments.Count == 0 ? null : string.Join(".", segments);
    }
}
