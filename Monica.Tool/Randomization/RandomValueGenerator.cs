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
        var count = byteCount.LimitInRange(1, 62);
        var bytes = new byte[count];
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
    public static double NextDouble(double minValue, double maxValue) =>
        Random.Shared.NextDouble() * (maxValue - minValue) + minValue;

    /// <summary>
    /// Generates a deterministic double in the supplied range using a hash seed.
    /// </summary>
    public static double NextDouble(double minValue, double maxValue, object hashSeed) =>
        ((hashSeed.GetHashCode() + Math.Abs((long)int.MinValue)) /
         (double)(int.MaxValue + Math.Abs((long)int.MinValue))) * (maxValue - minValue) + minValue;

    /// <summary>
    /// Generates a random integer in the supplied inclusive range.
    /// </summary>
    public static int NextInt(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            return minValue;
        }

        if (maxValue == int.MaxValue)
        {
            maxValue--;
        }

        return Random.Shared.Next(minValue, maxValue + 1);
    }

    /// <summary>
    /// Generates a deterministic integer in the supplied inclusive range using a hash seed.
    /// </summary>
    public static int NextInt(int minValue, int maxValue, object hashSeed)
    {
        if (minValue >= maxValue)
        {
            return minValue;
        }

        var modulus = maxValue - minValue + 1L;
        var remainder = (int)(Math.Abs(hashSeed.GetHashCode()) % modulus);
        return minValue + remainder;
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

        var range = (ulong)(maxValue - minValue) + 1;
        var rejectionThreshold = ulong.MaxValue - ((ulong.MaxValue % range) + 1) % range;
        ulong randomValue;

        do
        {
            var buffer = new byte[8];
            Random.Shared.NextBytes(buffer);
            randomValue = (ulong)BitConverter.ToInt64(buffer, 0);
        } while (randomValue > rejectionThreshold);

        return (long)(randomValue % range) + minValue;
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
}
