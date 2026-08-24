namespace Monica.Tool.Algorithms;

public class LevenshteinDistance
{
    /// <summary>
    /// Get similarity ratio of given two strings.
    /// </summary>
    /// <param name="str1"></param>
    /// <param name="str2"></param>
    /// <returns></returns>
    public static double Similarity(string str1, string str2)
    {
        ArgumentNullException.ThrowIfNull(str1);
        ArgumentNullException.ThrowIfNull(str2);

        double maxLength = Math.Max(str1.Length, str2.Length);
        if (maxLength == 0)
        {
            return 1;
        }

        return (maxLength - Calculate(str1, str2)) / maxLength;
    }
    /// <summary>
    /// Calculate the difference between 2 strings using the Levenshtein distance algorithm
    /// </summary>
    /// <param name="str1">First string</param>
    /// <param name="str2">Second string</param>
    /// <returns></returns>
    public static int Calculate(string str1, string str2) //O(n*m)
    {
        ArgumentNullException.ThrowIfNull(str1);
        ArgumentNullException.ThrowIfNull(str2);

        if (ReferenceEquals(str1, str2) || str1 == str2)
        {
            return 0;
        }

        // Keep the shorter string on the columns so memory usage is O(min(n, m)).
        if (str1.Length < str2.Length)
        {
            (str1, str2) = (str2, str1);
        }

        var len1 = str1.Length;
        var len2 = str2.Length;

        // First calculation, if one entry is empty return full length
        if (len1 == 0)
            return len2;

        if (len2 == 0)
            return len1;

        var previous = new int[len2 + 1];
        var current = new int[len2 + 1];
        for (var j = 0; j <= len2; j++)
        {
            previous[j] = j;
        }

        // Calculate rows and columns distances
        for (var i = 1; i <= len1; i++)
        {
            current[0] = i;
            for (var j = 1; j <= len2; j++)
            {
                var cost = str2[j - 1] == str1[i - 1] ? 0 : 1;

                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[len2];
    }
}
