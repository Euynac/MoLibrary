using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Monica.Generators.AutoController.Models;

/// <summary>
/// Value-based diagnostic data that does not retain a compilation, symbol, syntax tree, or Roslyn location.
/// </summary>
internal sealed class DiagnosticSnapshot : IEquatable<DiagnosticSnapshot>
{
    private DiagnosticSnapshot(
        DiagnosticDescriptor descriptor,
        SourceLocationSnapshot? location,
        ImmutableArray<string> arguments)
    {
        Descriptor = descriptor;
        Location = location;
        Arguments = arguments;
    }

    private DiagnosticDescriptor Descriptor { get; }

    private SourceLocationSnapshot? Location { get; }

    private ImmutableArray<string> Arguments { get; }

    public static DiagnosticSnapshot Create(
        DiagnosticDescriptor descriptor,
        Location? location,
        params object?[] arguments)
    {
        return new DiagnosticSnapshot(
            descriptor,
            SourceLocationSnapshot.Create(location),
            arguments.Select(static argument => Convert.ToString(argument, CultureInfo.InvariantCulture) ?? string.Empty)
                .ToImmutableArray());
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            Arguments.Cast<object>().ToArray());
    }

    public bool Equals(DiagnosticSnapshot? other)
    {
        return other is not null &&
               Descriptor.Id == other.Descriptor.Id &&
               Nullable.Equals(Location, other.Location) &&
               Arguments.SequenceEqual(other.Arguments, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is DiagnosticSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Descriptor.Id);
            hashCode = (hashCode * 397) ^ (Location?.GetHashCode() ?? 0);
            foreach (var argument in Arguments)
            {
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(argument);
            }

            return hashCode;
        }
    }
}

internal readonly struct SourceLocationSnapshot : IEquatable<SourceLocationSnapshot>
{
    private SourceLocationSnapshot(
        string filePath,
        int spanStart,
        int spanLength,
        int startLine,
        int startCharacter,
        int endLine,
        int endCharacter)
    {
        FilePath = filePath;
        SpanStart = spanStart;
        SpanLength = spanLength;
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
    }

    private string FilePath { get; }

    private int SpanStart { get; }

    private int SpanLength { get; }

    private int StartLine { get; }

    private int StartCharacter { get; }

    private int EndLine { get; }

    private int EndCharacter { get; }

    public static SourceLocationSnapshot? Create(Location? location)
    {
        if (location is null || !location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return new SourceLocationSnapshot(
            lineSpan.Path ?? string.Empty,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character,
            lineSpan.EndLinePosition.Line,
            lineSpan.EndLinePosition.Character);
    }

    public Location ToLocation()
    {
        return Location.Create(
            FilePath,
            new TextSpan(SpanStart, SpanLength),
            new LinePositionSpan(
                new LinePosition(StartLine, StartCharacter),
                new LinePosition(EndLine, EndCharacter)));
    }

    public bool Equals(SourceLocationSnapshot other)
    {
        return FilePath == other.FilePath &&
               SpanStart == other.SpanStart &&
               SpanLength == other.SpanLength &&
               StartLine == other.StartLine &&
               StartCharacter == other.StartCharacter &&
               EndLine == other.EndLine &&
               EndCharacter == other.EndCharacter;
    }

    public override bool Equals(object? obj)
    {
        return obj is SourceLocationSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(FilePath);
            hashCode = (hashCode * 397) ^ SpanStart;
            hashCode = (hashCode * 397) ^ SpanLength;
            hashCode = (hashCode * 397) ^ StartLine;
            hashCode = (hashCode * 397) ^ StartCharacter;
            hashCode = (hashCode * 397) ^ EndLine;
            hashCode = (hashCode * 397) ^ EndCharacter;
            return hashCode;
        }
    }
}
