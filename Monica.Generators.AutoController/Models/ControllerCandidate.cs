using System;
using System.Collections.Immutable;
using System.Linq;

namespace Monica.Generators.AutoController.Models;

internal sealed class ControllerCandidate : IEquatable<ControllerCandidate>
{
    public ControllerCandidate(EndpointModel endpoint, ImmutableArray<string> tags)
    {
        Endpoint = endpoint;
        Tags = tags;
    }

    public EndpointModel Endpoint { get; }

    public ImmutableArray<string> Tags { get; }

    public bool Equals(ControllerCandidate? other)
    {
        return other is not null &&
               Endpoint.Equals(other.Endpoint) &&
               Tags.SequenceEqual(other.Tags, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is ControllerCandidate other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Endpoint.GetHashCode();
            foreach (var tag in Tags)
            {
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(tag);
            }

            return hashCode;
        }
    }
}
