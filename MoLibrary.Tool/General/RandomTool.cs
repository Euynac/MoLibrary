using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Maths;

namespace MoLibrary.Tool.General
{
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

        #region 随机生成
        /// <summary>
        /// 获取强随机byte数组
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
                return randomFormat.RegexReplace(@"\$\{((?<normal>[lLn\s]+)|(?<date>date)|(?<datetime>datetime)|(?<time>time)|(?<id>id))\}", match =>
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
        ///生成随机字符串 
        ///</summary>
        ///<param name="length">目标字符串的长度</param>
        ///<param name="useNum">是否包含数字，默认包含</param>
        ///<param name="useLow">是否包含小写字母</param>
        ///<param name="useUpp">是否包含大写字母</param>
        ///<param name="useSpecial">是否包含特殊字符</param>
        ///<param name="custom">要包含的自定义字符，直接输入要包含的字符列表</param>
        ///<returns>指定长度的随机字符串</returns>
        public static string GetString(int length, bool useNum = true, bool useLow = false, bool useUpp = false, bool useSpecial = false, string? custom = null)
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

        #region ICollection的类拓展
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
            return hashString != null ?
                list[GetInt(0, list.Length - 1, hashString)]
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
            return hashString != null ? list.ElementAt(GetInt(0, list.Count - 1, hashString)) :
                list.ElementAt(_random.Next(list.Count));
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
            if (count == 1) return new[] { RandomGetOne(list, hashString)! };
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

        #region 时间类

        public static DateTime GetDateTimeBetween(DateTime left, DateTime right, TimeExtensions.DateTimePart truncateTo = TimeExtensions.DateTimePart.Second)
        {
            if (right < left) throw new ArgumentException("Right border must be greater than or equal to left border!", nameof(right));
            if (left == right) return left;

            var tickRange = right.Ticks - left.Ticks;
            if (tickRange <= 1) return left;

            var randomTime = new DateTime(left.Ticks + GetLong(0, tickRange));
            return TimeExtensions.Truncate(randomTime, truncateTo);
        }

        #endregion

        #region Enum类拓展
        /// <summary>
        /// 从Enum中随机选取一个（需要Enum类是0-n连续的）
        /// </summary>
        /// <returns></returns>
        public static T EnumRandomGetOne<T>() where T : Enum
        {
            var enumType = typeof(T);
            var length = Enum.GetNames(enumType).Length;
            return (T) Enum.Parse(enumType, _random.Next(length).ToString());
        }
        #endregion

        #region 数字类拓展

        /// <summary>
        /// 产生区间范围中的随机浮点数
        /// </summary>
        /// <param name="intervalDoublePair">区间</param>
        /// <returns></returns>
        public static double GetDouble(this IntervalDoublePair intervalDoublePair)
        {
            var rightInterval = intervalDoublePair.RightInterval;
            var leftInterval = intervalDoublePair.LeftInterval;
            var maxValue = rightInterval.NumType == NumberType.Infinity
                ? double.MaxValue
                : rightInterval.Value;
            var minValue = leftInterval.NumType == NumberType.Infinitesimal ? double.MinValue : leftInterval.Value;
            if (rightInterval.NumType == NumberType.Infinity || leftInterval.NumType == NumberType.Infinitesimal)
            {
                var randomFactor = _random.NextDouble();
                return minValue + randomFactor * maxValue - randomFactor * minValue;//如不这样可能会溢出
            }

            return _random.NextDouble() * (maxValue - minValue) + minValue;
        }
        /// <summary>
        /// Generate a random double between minValue and maxValue (include min but not max value).

        /// </summary>
        /// <returns></returns>
        public static double GetDouble(double minValue, double maxValue) => _random.NextDouble() * (maxValue - minValue) + minValue;
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
        /// 产生区间范围中的随机整数
        /// </summary>
        /// <param name="intervalDoublePair">区间</param>
        /// <returns></returns>
        public static int GetInt(this IntervalDoublePair intervalDoublePair)
        {
            var maxValue = intervalDoublePair.GetRightIntervalNearestNumber();
            var minValue = intervalDoublePair.GetLeftIntervalNearestNumber();
            if (minValue > maxValue) return minValue;//区间相同就麻烦了
            if (maxValue == int.MaxValue) maxValue--;
            return _random.Next(minValue, maxValue + 1);
        }
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

        #region object类拓展

        /// <summary>
        /// 有x%可能性返回null（用于 ?? 来随机选择，并较其他效率较高）
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="probability">概率基础值，不返回null的可能性[0-1]</param>
        /// <param name="influenceValue">影响值，与原始基础概率相加，可为负</param>
        /// <param name="maxProbability">最大可能性，影响值+基础值的最大值</param>
        /// <param name="minProbability">最小可能性，影响值+基础值的最小值</param>
        /// <returns></returns>
        public static T? ProbablyNull<T>(this T obj, double probability, double influenceValue = 0, double maxProbability = 1, double minProbability = 0) where T : class
            => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? null : obj;

        /// <summary>
        /// 有x%可能性不返回null，就是有x%可能性会做（链式上?截断用于概率执行）
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="probability">概率基础值，不返回null的可能性[0-1]</param>
        /// <param name="influenceValue">影响值，与原始基础概率相加，可为负</param>
        /// <param name="maxProbability">最大可能性，影响值+基础值的最大值</param>
        /// <param name="minProbability">最小可能性，影响值+基础值的最小值</param>
        /// <returns></returns>
        public static T? ProbablyDo<T>(this T obj, double probability, double influenceValue = 0, double maxProbability = 1, double minProbability = 0) where T : class
            => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? obj : null;

        /// <summary>
        /// 有x%可能性会成为给定的对象
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="become">x%可能会返回的对象</param>
        /// <param name="probability">概率基础值，不返回null的可能性[0-1]</param>
        /// <param name="influenceValue">影响值，与原始基础概率相加，可为负</param>
        /// <param name="maxProbability">最大可能性，影响值+基础值的最大值</param>
        /// <param name="minProbability">最小可能性，影响值+基础值的最小值</param>
        /// <returns>没有成为就返回原先的对象</returns>
        public static T ProbablyBe<T>(this T obj, T become, double probability, double influenceValue = 0, double maxProbability = 1, double minProbability = 0)
            => ProbablyTrue(probability, influenceValue, maxProbability, minProbability) ? become : obj;

        /// <summary>
        /// 有x%可能性返回true
        /// </summary>
        /// <param name="probability">概率基础值，返回true的可能性[0-1]</param>
        /// <param name="influenceValue">影响值，与原始基础概率相加，可为负</param>
        /// <param name="maxProbability">0-1之间 最大可能性，影响值+基础值的最大值</param>
        /// <param name="minProbability">0-1之间 最小可能性，影响值+基础值的最小值</param>
        /// <returns></returns>
        public static bool ProbablyTrue(this double probability, double influenceValue = 0, double maxProbability = 1, double minProbability = 0)
        {
            var finalProbability = (probability + influenceValue).LimitInRange(minProbability, maxProbability);
            return _random.NextDouble() < finalProbability;
        }

        /// <summary>
        /// 结果基于给定的哈希，有x%可能性返回true
        /// </summary>
        /// <param name="probability">概率基础值，返回true的可能性[0-1]</param>
        /// <param name="objectToHash"></param>
        /// <param name="influenceValue">影响值，与原始基础概率相加，可为负</param>
        /// <param name="maxProbability">0-1之间 最大可能性，影响值+基础值的最大值</param>
        /// <param name="minProbability">0-1之间 最小可能性，影响值+基础值的最小值</param>
        /// <returns></returns>
        public static bool ProbablyTrue(this double probability, object objectToHash, double influenceValue = 0, double maxProbability = 1,
            double minProbability = 0)
        {
            var finalProbability = (probability + influenceValue).LimitInRange(minProbability, maxProbability);
            return GetDouble(0, 1, objectToHash) < finalProbability;
        }

        #endregion

        #region 数学分布

        public static double DistributeUsePowerFunction(int value, double distributeMaxValue, double power, int maxRange = 100)
        {
            var factor = Math.Pow(distributeMaxValue, 1 / power) / maxRange;
            return Math.Pow(factor * value, power);
        }

        #endregion



    }
}
