using System.Security.Cryptography;
using System.Text;
using Monica.Tool.Extensions;

namespace Monica.Tool.General;

/// <summary>
/// Random Tool (Thread-safe)
/// </summary>
public static class RandomTool
{
    // Cache for character pools to avoid repeated string concatenation
    private static readonly string _numberPool = "0123456789";
    private static readonly string _lowerPool = "abcdefghijklmnopqrstuvwxyz";
    private static readonly string _upperPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string _specialPool = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    /// <summary>
    /// Thread-safe random instance with null safety
    /// </summary>
    private static readonly Random _random = Random.Shared;

    #region Random Generation

    /// <summary>
    /// Gets a cryptographically secure random byte array.
    /// </summary>
    /// <param name="byteCount"></param>
    /// <returns></returns>
    public static byte[] GetSecurityRandomByte(int byteCount = 1)
    {
        var count = byteCount.LimitInRange(1, 62);
        var b = new byte[count];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(b);
        return b;
    }

    /// <summary>
    /// Use placeholder to generate random string. e.g. lll LLLnn means abc ABC12.
    /// </summary>
    /// <param name="randomFormat">l:lower letter
    /// <para>L:Upper letter</para>
    /// <para>n:number</para>
    /// </param>
    /// <param name="strict">Use strict pattern: ${id} ${lll nnn} nn ${nn}</param>
    /// <param name="id">Strict pattern support ${id}, each iterate need ++.</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetString(string randomFormat, bool strict = false, int id = 0)
    {
        if (!strict) return EasyGet(randomFormat);
        {
            return randomFormat.RegexReplace(
                @"\$\{((?<normal>[lLn\s]+)|(?<date>date)|(?<datetime>datetime)|(?<time>time)|(?<id>id))\}", match =>
                {
                    if (match.Groups["normal"].Success)
                    {
                        return EasyGet(match.Groups["normal"].Value);
                    }

                    if (match.Groups["date"].Success)
                    {
                        return GetDateTimeBetween(DateTime.Today, DateTime.Today.Add(TimeSpan.FromDays(365)))
                            .ToString("d");
                    }

                    if (match.Groups["datetime"].Success)
                    {
                        return GetDateTimeBetween(DateTime.Today, DateTime.Today.Add(TimeSpan.FromDays(365)))
                            .ToString("G");
                    }

                    if (match.Groups["time"].Success)
                    {
                        return GetDateTimeBetween(DateTime.Today, DateTime.Today.Add(TimeSpan.FromDays(365)))
                            .ToString("HH:mm:ss");
                    }

                    if (match.Groups["id"].Success)
                    {
                        return id.ToString();
                    }

                    throw new Exception("Not supported strict pattern");
                });
        }

        static string EasyGet(string randomStr)
        {
            return randomStr.RegexReplace("(l+|L+|n+)", match =>
            {
                var matched = match.Value;
                return matched[0] switch
                {
                    'l' => GetString(matched.Length, false, useLow: true),
                    'L' => GetString(matched.Length, false, useUpp: true),
                    'n' => GetString(matched.Length),
                    _ => throw new Exception("No supported placeholder")
                };
            });
        }
    }


    ///<summary>
    /// Generates a random string.
    ///</summary>
    ///<param name="length">Target string length.</param>
    ///<param name="useNum">Whether to include digits. Included by default.</param>
    ///<param name="useLow">Whether to include lowercase letters.</param>
    ///<param name="useUpp">Whether to include uppercase letters.</param>
    ///<param name="useSpecial">Whether to include special characters.</param>
    ///<param name="custom">Custom characters to include directly.</param>
    ///<returns>A random string of the specified length.</returns>
    public static string GetString(int length, bool useNum = true, bool useLow = false, bool useUpp = false,
        bool useSpecial = false, string? custom = null)
    {
        if (length <= 0) return string.Empty;

        var randomPool = BuildCharacterPool(useNum, useLow, useUpp, useSpecial, custom);
        if (randomPool.Length == 0) return string.Empty;

        var result = new char[length];
        var random = _random;

        for (int i = 0; i < length; i++)
        {
            result[i] = randomPool[random.Next(randomPool.Length)];
        }

        return new string(result);
    }

    private static string BuildCharacterPool(bool useNum, bool useLow, bool useUpp, bool useSpecial, string? custom)
    {
        var poolBuilder = new StringBuilder(custom ?? "");

        if (useNum) poolBuilder.Append(_numberPool);
        if (useLow) poolBuilder.Append(_lowerPool);
        if (useUpp) poolBuilder.Append(_upperPool);
        if (useSpecial) poolBuilder.Append(_specialPool);

        return poolBuilder.ToString();
    }

    #endregion

    #region ICollection Extensions

    /// <summary>
    /// Get a random item from the array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="candidates"></param>
    /// <returns></returns>
    public static T GetOne<T>(params T[] candidates) => candidates.RandomGetOne()!;

    /// <summary>
    /// Get a number of random items from T[] (no duplicates).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="candidates"></param>
    /// <param name="count">Count is already automatically limited to 1-list.Count</param>
    /// <returns></returns>
    public static IEnumerable<T>? Get<T>(int count, params T[] candidates)
    {
        Random.Shared.GetItems<T>(candidates.AsSpan(), 4);
        return RandomGet(candidates.ToArray(), count);
    }

    /// <summary>
    /// Get a random item from given items.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="candidates"></param>
    /// <returns></returns>
    public static T? RandomGetOneFrom<T>(params T?[]? candidates) => candidates.RandomGetOne();

    /// <summary>
    /// Get a number of random items from given items (no duplicates).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>return null when the set is null</returns>
    public static IEnumerable<T>? RandomGetFrom<T>(int count, params T[]? candidates) => candidates?.RandomGet(count);

    /// <summary>
    /// Get a random item from the array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="hashString">if use hash, then the result depends on it</param>
    /// <returns>return default(T) if fails</returns>
    public static T? RandomGetOne<T>(this T[]? list, string? hashString = null)
    {
        if (list == null || !list.Any()) return default;
        return hashString != null
            ? list[GetInt(0, list.Length - 1, hashString)]
            : list[_random.Next(list.Length)];
    }

    /// <summary>
    /// Get a random item from the <see cref="ICollection&lt;T&gt;"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="hashString">if use hash, then the result depends on it</param>
    /// <returns>return default(T) if fails</returns>
    public static T? RandomGetOne<T>(this ICollection<T>? list, string? hashString = null)
    {
        if (list == null || !list.Any()) return default;
        return hashString != null
            ? list.ElementAt(GetInt(0, list.Count - 1, hashString))
            : list.ElementAt(_random.Next(list.Count));
    }

    /// <summary>
    /// Get a number of random items from <see cref="IEnumerable&lt;T&gt;"/> (no duplicates).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="count">Count is already automatically limited to 1-list.Count</param>
    /// <param name="hashString">if use hash, then the result depends on it</param>
    /// <returns>return null when the set is null</returns>
    public static IEnumerable<T>? RandomGet<T>(this ICollection<T>? list, int count, string? hashString = null)
    {
        if (list == null || list.Count == 0) return null;
        if (count <= 0) return Enumerable.Empty<T>();
        if (count == 1) return new[] {RandomGetOne(list, hashString)!};
        if (count >= list.Count) return RandomList(list, hashString);

        // Use Fisher-Yates shuffle for better performance with large collections
        if (hashString == null && list.Count > 100)
        {
            return FisherYatesShuffle(list).Take(count);
        }

        return hashString != null
            ? list.OrderBy(_ => hashString.GetHashCode()).Take(count)
            : list.OrderBy(_ => Guid.NewGuid()).Take(count);
    }

    private static IEnumerable<T> FisherYatesShuffle<T>(ICollection<T> source)
    {
        var array = source.ToArray();
        var random = _random;

        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }

        return array;
    }

    /// <summary>
    /// Return <see cref="IEnumerable&lt;T&gt;"/> in disordered order, or return the original data if it fails
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="hashString">if use hash, then the result depends on it</param>
    /// <returns></returns>
    public static IEnumerable<T>? RandomList<T>(this ICollection<T>? list, string? hashString = null)
    {
        if (list == null || list.Count == 0) return list;

        // Use Fisher-Yates for better performance with large collections
        if (hashString == null && list.Count > 50)
        {
            return FisherYatesShuffle(list);
        }

        return hashString != null
            ? list.OrderBy(o => hashString.GetHashCode())
            : list.OrderBy(o => Guid.NewGuid());
    }

    #endregion

    #region Time Extensions

    public static DateTime GetDateTimeBetween(DateTime left, DateTime right,
        TimeExtensions.DateTimePart truncateTo = TimeExtensions.DateTimePart.Second)
    {
        if (right < left)
            throw new ArgumentException("Right border must be greater than or equal to left border!", nameof(right));
        if (left == right) return left;

        var tickRange = right.Ticks - left.Ticks;
        if (tickRange <= 1) return left;

        var randomTime = new DateTime(left.Ticks + GetLong(0, tickRange));
        return TimeExtensions.Truncate(randomTime, truncateTo);
    }

    #endregion

    #region Enum Extensions

    /// <summary>
    /// Picks one random value from an enum (requires enum values to be continuous from 0 to n).
    /// </summary>
    /// <returns></returns>
    public static T EnumRandomGetOne<T>() where T : Enum
    {
        var enumType = typeof(T);
        var length = Enum.GetNames(enumType).Length;
        return (T) Enum.Parse(enumType, _random.Next(length).ToString());
    }

    #endregion

    #region Numeric Extensions

    /// <summary>
    /// Generate a random double between minValue and maxValue (include min but not max value).
    /// </summary>
    /// <returns></returns>
    public static double GetDouble(double minValue, double maxValue) =>
        _random.NextDouble() * (maxValue - minValue) + minValue;

    /// <summary>
    /// Generate a random double between minValue and maxValue base on hash code (include min but not max value).
    /// </summary>
    /// <param name="minValue"></param>
    /// <param name="maxValue"></param>
    /// <param name="objectToHash"></param>
    /// <returns></returns>
    public static double GetDouble(double minValue, double maxValue, object objectToHash) =>
        ((objectToHash.GetHashCode() + Math.Abs((long) int.MinValue)) /
         (double) (int.MaxValue + Math.Abs((long) int.MinValue))) * (maxValue - minValue) + minValue;

    /// <summary>
    /// Generate a random integer between minValue and maxValue (include min and max value).
    /// </summary>
    /// <returns></returns>
    public static int GetInt(int minValue, int maxValue)
    {
        if (minValue >= maxValue) return minValue;
        if (maxValue == int.MaxValue) maxValue--;
        return _random.Next(minValue, maxValue + 1);
    }

    /// <summary>
    /// Generate a random integer between minValue and maxValue base on hash code (include min and max value).
    /// </summary>
    /// <returns></returns>
    ///https://stackoverflow.com/a/29811247/18731746
    public static int GetInt(int minValue, int maxValue, object objectToHash)
    {
        if (minValue >= maxValue) return minValue;
        long modular = maxValue - minValue + 1;
        var hashCode = Math.Abs(objectToHash.GetHashCode());
        var rest = (int) (hashCode % modular);
        return minValue + rest;
    }

    /// <summary>
    /// Generate a random long integer between minValue and maxValue (include min and max value).
    /// </summary>
    /// <returns></returns>
    public static long GetLong(long minValue, long maxValue)
    {
        if (maxValue < minValue)
            throw new ArgumentOutOfRangeException(nameof(maxValue), "max must be >= min!");
        if (maxValue == minValue) return maxValue;

        var uRange = (ulong) (maxValue - minValue);
        if (uRange == 0) return minValue;

        // Prevent modulo bias using rejection sampling
        ulong ulongRand;
        var threshold = ulong.MaxValue - (ulong.MaxValue % uRange + 1) % uRange;

        do
        {
            var buf = new byte[8];
            _random.NextBytes(buf);
            ulongRand = (ulong) BitConverter.ToInt64(buf, 0);
        } while (ulongRand > threshold);

        return (long) (ulongRand % uRange) + minValue;
    }

    #endregion

    #region Object Extensions

    /// <summary>
    /// Returns null with x% probability (useful with ?? for probabilistic selection with good performance).
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="probability">Base probability in [0-1], representing the chance of not returning null.</param>
    /// <param name="influenceValue">Influence value added to the base probability; can be negative.</param>
    /// <param name="maxProbability">Upper bound for (base + influence).</param>
    /// <param name="minProbability">Lower bound for (base + influence).</param>
    /// <returns></returns>
    public static T? ProbablyNull<T>(this T obj, double probability, double influenceValue = 0,
        double maxProbability = 1, double minProbability = 0) where T : class
        => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? null : obj;

    /// <summary>
    /// Returns the original object with x% probability; otherwise returns null.
    /// Useful for probabilistic execution in nullable chaining.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="probability">Base probability in [0-1], representing the chance of not returning null.</param>
    /// <param name="influenceValue">Influence value added to the base probability; can be negative.</param>
    /// <param name="maxProbability">Upper bound for (base + influence).</param>
    /// <param name="minProbability">Lower bound for (base + influence).</param>
    /// <returns></returns>
    public static T? ProbablyDo<T>(this T obj, double probability, double influenceValue = 0, double maxProbability = 1,
        double minProbability = 0) where T : class
        => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? obj : null;

    /// <summary>
    /// Returns the provided replacement object with x% probability.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="become">Replacement object returned with the configured probability.</param>
    /// <param name="probability">Base probability in [0-1].</param>
    /// <param name="influenceValue">Influence value added to the base probability; can be negative.</param>
    /// <param name="maxProbability">Upper bound for (base + influence).</param>
    /// <param name="minProbability">Lower bound for (base + influence).</param>
    /// <returns>The replacement object if triggered; otherwise the original object.</returns>
    public static T ProbablyBe<T>(this T obj, T become, double probability, double influenceValue = 0,
        double maxProbability = 1, double minProbability = 0)
        => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? become : obj;

    /// <summary>
    /// Returns true with x% probability.
    /// </summary>
    /// <param name="probability">Base probability of returning true in [0-1].</param>
    /// <param name="influenceValue">Influence value added to the base probability; can be negative.</param>
    /// <param name="maxProbability">Upper bound in [0-1] for (base + influence).</param>
    /// <param name="minProbability">Lower bound in [0-1] for (base + influence).</param>
    /// <returns></returns>
    public static bool ProbablyTrue(this double probability, double influenceValue = 0, double maxProbability = 1,
        double minProbability = 0)
    {
        var finalProbability = (probability + influenceValue).LimitInRange(minProbability, maxProbability);
        return _random.NextDouble() < finalProbability;
    }

    /// <summary>
    /// Returns true with x% probability, deterministically based on the provided hash source.
    /// </summary>
    /// <param name="probability">Base probability of returning true in [0-1].</param>
    /// <param name="objectToHash"></param>
    /// <param name="influenceValue">Influence value added to the base probability; can be negative.</param>
    /// <param name="maxProbability">Upper bound in [0-1] for (base + influence).</param>
    /// <param name="minProbability">Lower bound in [0-1] for (base + influence).</param>
    /// <returns></returns>
    public static bool ProbablyTrue(this double probability, object objectToHash, double influenceValue = 0,
        double maxProbability = 1,
        double minProbability = 0)
    {
        var finalProbability = (probability + influenceValue).LimitInRange(minProbability, maxProbability);
        return GetDouble(0, 1, objectToHash) < finalProbability;
    }

    #endregion

    #region Mathematical Distribution

    public static double DistributeUsePowerFunction(int value, double distributeMaxValue, double power,
        int maxRange = 100)
    {
        var factor = Math.Pow(distributeMaxValue, 1 / power) / maxRange;
        return Math.Pow(factor * value, power);
    }

    #endregion
}
