using System;
using System.Collections.Generic;
using System.Linq;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.Maths;

public static class KouMath
{
    /// <summary>
    /// Please note that C# and C++'s % operator is actually NOT a modulo, it's remainder. -4 % 3 = -1 and this will -4 % 3 = 2
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    /// https://stackoverflow.com/questions/1082917/mod-of-negative-number-is-melting-my-brain
    public static double Mod(double a, double b) => a - b * Math.Floor(a / b);

    public static bool IsOdd(this int number) => number % 2 == 1;
    public static bool IsEven(this int number) => number % 2 == 0;
    /// <summary>
    /// <para>https://www.rookieslab.com/posts/fastest-way-to-check-if-a-number-is-prime-or-not</para>
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public static bool IsPrime(this long n)
    {
        if (n <= 0) return false;
        // Assumes that n is a positive natural number
        // We know 1 is not a prime number
        if (n == 1)
        {
            return false;
        }

        long i = 2;
        // This will loop from 2 to int(sqrt(x))
        while (i * i <= n)
        {
            // Check if i divides x without leaving a remainder
            if (n % i == 0)
            {
                // This means that n has a factor in between 2 and sqrt(n)
                // So it is not a prime number
                return false;
            }
            i += 1;
        }
        // If we did not find any factor in the above loop,
        // then n is a prime number
        return true;
    }

    public static double Mode(this IEnumerable<double> numbers)
    {
        var groups = numbers.GroupBy(v => v).ToList();
        var maxCount = groups.Max(g => g.Count());
        return groups.First(g => g.Count() == maxCount).Key;
    }

    public static double Median(this IEnumerable<double> numbers)
    {
        if(numbers.IsNullOrEmptySet()) return 0;
        var sortedNumbers = numbers.ToList();
        sortedNumbers.Sort();
       
        var size = sortedNumbers.Count;
        var mid = size / 2;
        var median = (size % 2 != 0) ? sortedNumbers[mid] : (sortedNumbers[mid] + sortedNumbers[mid - 1]) / 2.0;
        return median;
    }
}