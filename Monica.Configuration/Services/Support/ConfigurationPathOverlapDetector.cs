using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Detects whether two Microsoft configuration paths address the same node or an ancestor/descendant pair.
/// </summary>
internal static class ConfigurationPathOverlapDetector
{
    /// <summary>
    /// Determines whether applying mutations at both paths would make their command order observable.
    /// </summary>
    /// <param name="left">The first Microsoft configuration path.</param>
    /// <param name="right">The second Microsoft configuration path.</param>
    /// <returns><see langword="true"/> when either path is equal to or contains the other path.</returns>
    public static bool Overlaps(string left, string right)
    {
        var leftSegments = Split(left);
        var rightSegments = Split(right);
        var sharedLength = Math.Min(leftSegments.Length, rightSegments.Length);

        for (var index = 0; index < sharedLength; index++)
        {
            if (!string.Equals(leftSegments[index], rightSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // An empty path represents the root and therefore overlaps every descendant.
        return true;
    }

    /// <summary>
    /// Determines whether one Microsoft configuration path is a strict ancestor of the other.
    /// </summary>
    /// <param name="left">The first Microsoft configuration path.</param>
    /// <param name="right">The second Microsoft configuration path.</param>
    /// <returns><see langword="true"/> for ancestor/descendant paths, excluding equal paths.</returns>
    public static bool HasStrictContainment(string left, string right)
    {
        var leftSegments = Split(left);
        var rightSegments = Split(right);
        return leftSegments.Length != rightSegments.Length && Overlaps(left, right);
    }

    /// <summary>
    /// Determines whether one logical path is a strict ancestor of the other.
    /// </summary>
    /// <param name="left">The first logical path.</param>
    /// <param name="right">The second logical path.</param>
    /// <returns><see langword="true"/> for ancestor/descendant paths, excluding equal paths.</returns>
    public static bool HasStrictContainment(LogicalPath left, LogicalPath right)
    {
        if (left.Depth == right.Depth)
        {
            return false;
        }

        var sharedLength = Math.Min(left.Depth, right.Depth);
        for (var index = 0; index < sharedLength; index++)
        {
            var leftSegment = left.Segments[index];
            var rightSegment = right.Segments[index];
            if (leftSegment.GetType() != rightSegment.GetType()
                || !string.Equals(leftSegment.Value, rightSegment.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Split(string path)
    {
        return path.Split(':', StringSplitOptions.RemoveEmptyEntries);
    }
}
