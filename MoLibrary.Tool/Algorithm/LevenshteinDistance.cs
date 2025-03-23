using System;

namespace MoLibrary.Tool.Algorithm;

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
        double maxLength = Math.Max(str1.Length, str2.Length);
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
        var len1 = str1.Length;
        var len2 = str2.Length;

        var matrix = new int[len1 + 1, len2 + 1];

        // First calculation, if one entry is empty return full length
        if (len1 == 0)
            return len2;

        if (len2 == 0)
            return len1;

        // Initialization of matrix with row size source1Length and columns size source2Length
        for (var i = 0; i <= len1; matrix[i, 0] = i++){}
        for (var j = 0; j <= len2; matrix[0, j] = j++){}

        // Calculate rows and columns distances
        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = str2[j - 1] == str1[i - 1] ? 0 : 1;
                
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        // return result
        return matrix[len1, len2];
    }
}