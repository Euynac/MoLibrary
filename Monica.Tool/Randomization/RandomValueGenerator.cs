using System.Security.Cryptography;
using System.Text;
using Monica.Tool.Extensions;

namespace Monica.Tool.Randomization;

/// <summary>
/// Generates random values for bytes, numbers, strings, dates, and enum members.
/// </summary>
public static class RandomValueGenerator
{
    private const string NumberCharacters = "0123456789";
    private const string LowercaseCharacters = "abcdefghijklmnopqrstuvwxyz";
    private const string UppercaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string SpecialCharacters = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    /// <summary>
    /// Returns cryptographically secure random bytes.
    /// </summary>
    public static byte[] NextSecureBytes(int byteCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);

        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    /// <summary>
    /// Generates a random string from the supplied placeholder format.
    /// </summary>
    public static string NextString(string format, bool strict = false, int id = 0)
    {
        if (!strict)
        {
            return ExpandSimplePattern(format);
        }

        return format.RegexReplace(
            @"\$\{((?<normal>[lLn\s]+)|(?<date>date)|(?<datetime>datetime)|(?<time>time)|(?<id>id))\}",
            match =>
            {
                if (match.Groups["normal"].Success)
                {
                    return ExpandSimplePattern(match.Groups["normal"].Value);
                }

                if (match.Groups["date"].Success)
                {
                    return NextDateTime(DateTime.Today, DateTime.Today.AddDays(365)).ToString("d");
                }

                if (match.Groups["datetime"].Success)
                {
                    return NextDateTime(DateTime.Today, DateTime.Today.AddDays(365)).ToString("G");
                }

                if (match.Groups["time"].Success)
                {
                    return NextDateTime(DateTime.Today, DateTime.Today.AddDays(365)).ToString("HH:mm:ss");
                }

                if (match.Groups["id"].Success)
                {
                    return id.ToString();
                }

                throw new InvalidOperationException("Unsupported strict placeholder pattern.");
            });
    }

    /// <summary>
    /// Generates a random string with the requested character groups.
    /// </summary>
    public static string NextString(int length, bool includeNumbers = true, bool includeLowercase = false,
        bool includeUppercase = false, bool includeSpecialCharacters = false, string? customCharacters = null)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        var characterPool = BuildCharacterPool(includeNumbers, includeLowercase, includeUppercase,
            includeSpecialCharacters, customCharacters);
        if (characterPool.Length == 0)
        {
            return string.Empty;
        }

        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = characterPool[Random.Shared.Next(characterPool.Length)];
        }

        return new string(result);
    }

    /// <summary>
    /// Generates a random <see cref="DateTime"/> between the supplied boundaries.
    /// </summary>
    public static DateTime NextDateTime(DateTime minValue, DateTime maxValue,
        TimeExtensions.DateTimePart truncateTo = TimeExtensions.DateTimePart.Second)
    {
        if (maxValue < minValue)
        {
            throw new ArgumentException("maxValue must be greater than or equal to minValue.", nameof(maxValue));
        }

        if (minValue == maxValue)
        {
            return minValue;
        }

        var tickRange = maxValue.Ticks - minValue.Ticks;
        if (tickRange <= 1)
        {
            return minValue;
        }

        var randomTime = new DateTime(minValue.Ticks + NextLong(0, tickRange));
        return TimeExtensions.Truncate(randomTime, truncateTo);
    }

    /// <summary>
    /// Returns one random enum value.
    /// </summary>
    public static T NextEnumValue<T>() where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        return values[Random.Shared.Next(values.Length)];
    }

    /// <summary>
    /// Generates a random double in the supplied range.
    /// </summary>
    public static double NextDouble(double minValue, double maxValue)
    {
        ValidateDoubleRange(minValue, maxValue);
        return Interpolate(minValue, maxValue, Random.Shared.NextDouble());
    }

    /// <summary>
    /// Generates a deterministic double in the supplied range using a hash seed.
    /// </summary>
    public static double NextDouble(double minValue, double maxValue, object hashSeed)
    {
        ArgumentNullException.ThrowIfNull(hashSeed);
        ValidateDoubleRange(minValue, maxValue);

        var normalizedHash = unchecked((uint)(hashSeed.GetHashCode() - int.MinValue));
        var fraction = normalizedHash / ((double)uint.MaxValue + 1);
        return Interpolate(minValue, maxValue, fraction);
    }

    /// <summary>
    /// Generates a random integer in the supplied inclusive range.
    /// </summary>
    public static int NextInt(int minValue, int maxValue)
    {
        return checked((int)NextLong(minValue, maxValue));
    }

    /// <summary>
    /// Generates a deterministic integer in the supplied inclusive range using a hash seed.
    /// </summary>
    public static int NextInt(int minValue, int maxValue, object hashSeed)
    {
        ArgumentNullException.ThrowIfNull(hashSeed);

        if (maxValue < minValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be greater than or equal to minValue.");
        }

        var range = (ulong)((long)maxValue - minValue) + 1;
        var normalizedHash = unchecked((uint)(hashSeed.GetHashCode() - int.MinValue));
        var offset = normalizedHash % range;
        return (int)(minValue + (long)offset);
    }

    /// <summary>
    /// Generates a random long in the supplied inclusive range.
    /// </summary>
    public static long NextLong(long minValue, long maxValue)
    {
        if (maxValue < minValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be greater than or equal to minValue.");
        }

        if (maxValue == minValue)
        {
            return maxValue;
        }

        var range = unchecked((ulong)maxValue - (ulong)minValue + 1);
        var randomValue = NextUInt64();

        if (range == 0)
        {
            return unchecked((long)randomValue);
        }

        var rejectionThreshold = unchecked(0UL - range) % range;
        while (randomValue < rejectionThreshold)
        {
            randomValue = NextUInt64();
        }

        var offset = randomValue % range;
        return unchecked((long)((ulong)minValue + offset));
    }

    private static string ExpandSimplePattern(string format) =>
        format.RegexReplace(
            "(l+|L+|n+)",
            match => match.Value[0] switch
            {
                'l' => NextString(match.Value.Length, includeNumbers: false, includeLowercase: true),
                'L' => NextString(match.Value.Length, includeNumbers: false, includeUppercase: true),
                'n' => NextString(match.Value.Length),
                _ => throw new InvalidOperationException("Unsupported placeholder character.")
            });

    private static string BuildCharacterPool(bool includeNumbers, bool includeLowercase, bool includeUppercase,
        bool includeSpecialCharacters, string? customCharacters)
    {
        var builder = new StringBuilder(customCharacters ?? string.Empty);
        if (includeNumbers)
        {
            builder.Append(NumberCharacters);
        }

        if (includeLowercase)
        {
            builder.Append(LowercaseCharacters);
        }

        if (includeUppercase)
        {
            builder.Append(UppercaseCharacters);
        }

        if (includeSpecialCharacters)
        {
            builder.Append(SpecialCharacters);
        }

        return builder.ToString();
    }

    private static ulong NextUInt64()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        Random.Shared.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }

    private static void ValidateDoubleRange(double minValue, double maxValue)
    {
        if (!double.IsFinite(minValue))
        {
            throw new ArgumentOutOfRangeException(nameof(minValue), "The range boundary must be finite.");
        }

        if (!double.IsFinite(maxValue) || maxValue < minValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be finite and greater than or equal to minValue.");
        }
    }

    private static double Interpolate(double minValue, double maxValue, double fraction)
    {
        if (minValue == maxValue)
        {
            return minValue;
        }

        return minValue * (1 - fraction) + maxValue * fraction;
    }
}
