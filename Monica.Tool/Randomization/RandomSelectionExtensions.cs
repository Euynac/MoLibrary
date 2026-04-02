namespace Monica.Tool.General;

/// <summary>
/// Provides helpers for random selection and shuffling over in-memory collections.
/// </summary>
public static class RandomSelectionExtensions
{
    /// <summary>
    /// Returns one random value from the supplied candidates.
    /// </summary>
    public static T PickRandom<T>(params T[] candidates) => candidates.PickRandom()!;

    /// <summary>
    /// Returns multiple random values from the supplied candidates.
    /// </summary>
    public static IEnumerable<T>? PickRandomMany<T>(int count, params T[]? candidates) => candidates?.PickRandomMany(count);

    /// <summary>
    /// Returns one random value from the array, or <c>default</c> when the array is empty.
    /// </summary>
    public static T? PickRandom<T>(this T[]? source, object? hashSeed = null)
    {
        if (source == null || source.Length == 0)
        {
            return default;
        }

        return source[CreateRandom(hashSeed).Next(source.Length)];
    }

    /// <summary>
    /// Returns one random value from the collection, or <c>default</c> when the collection is empty.
    /// </summary>
    public static T? PickRandom<T>(this ICollection<T>? source, object? hashSeed = null)
    {
        if (source == null || source.Count == 0)
        {
            return default;
        }

        return source.ElementAt(CreateRandom(hashSeed).Next(source.Count));
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> random values from the collection.
    /// </summary>
    public static IEnumerable<T>? PickRandomMany<T>(this ICollection<T>? source, int count, object? hashSeed = null)
    {
        if (source == null || source.Count == 0)
        {
            return null;
        }

        if (count <= 0)
        {
            return Enumerable.Empty<T>();
        }

        if (count >= source.Count)
        {
            return source.ShuffleRandom(hashSeed);
        }

        return Shuffle(source, CreateRandom(hashSeed)).Take(count);
    }

    /// <summary>
    /// Returns the collection in randomized order.
    /// </summary>
    public static IEnumerable<T>? ShuffleRandom<T>(this ICollection<T>? source, object? hashSeed = null)
    {
        if (source == null || source.Count == 0)
        {
            return source;
        }

        return Shuffle(source, CreateRandom(hashSeed));
    }

    private static T[] Shuffle<T>(ICollection<T> source, Random random)
    {
        var array = source.ToArray();
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        return array;
    }

    private static Random CreateRandom(object? hashSeed) => hashSeed == null ? Random.Shared : new Random(hashSeed.GetHashCode());
}
