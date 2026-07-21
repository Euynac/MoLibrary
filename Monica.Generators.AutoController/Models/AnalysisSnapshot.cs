using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Monica.Generators.AutoController.Models;

/// <summary>
/// Immutable semantic-analysis output suitable for Roslyn incremental caching.
/// </summary>
internal sealed class AnalysisSnapshot<T> : IEquatable<AnalysisSnapshot<T>>
    where T : class, IEquatable<T>
{
    public AnalysisSnapshot(T? value, IEnumerable<DiagnosticSnapshot>? diagnostics = null)
    {
        Value = value;
        Diagnostics = diagnostics?.ToImmutableArray() ?? ImmutableArray<DiagnosticSnapshot>.Empty;
    }

    public T? Value { get; }

    public ImmutableArray<DiagnosticSnapshot> Diagnostics { get; }

    public bool Equals(AnalysisSnapshot<T>? other)
    {
        return other is not null &&
               EqualityComparer<T?>.Default.Equals(Value, other.Value) &&
               Diagnostics.SequenceEqual(other.Diagnostics);
    }

    public override bool Equals(object? obj)
    {
        return obj is AnalysisSnapshot<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Value?.GetHashCode() ?? 0;
            foreach (var diagnostic in Diagnostics)
            {
                hashCode = (hashCode * 397) ^ diagnostic.GetHashCode();
            }

            return hashCode;
        }
    }
}
