using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Maths;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Koubot.Tool.KouData;

namespace Koubot.Tool.String
{
    /// <summary>
    /// Kou开发常用字符串工具类
    /// </summary>
    public static class KouStringTool
    {

      


        #region KouType类型适配
        /// <summary>
        /// 将字符串类型的数字转换为bool类型，支持中文以及英文、数字
        /// </summary>
        /// <param name="str"></param>
        /// <param name="boolResult"></param>
        /// <param name="kouType"></param>
        /// <returns></returns>
        public static bool TryToBool(string str, out bool boolResult, bool kouType = true)
        {
            boolResult = false;
            if (str.IsNullOrWhiteSpace()) return false;
            return !kouType ? bool.TryParse(str, out boolResult) :
                KouStaticData.StringToBoolDict.TryGetValue(str, out boolResult);
        }

        /// <summary>
        /// 将字符串类型的数字转换为enum类型，支持KouEnumName标签特性别名枚举
        /// </summary>
        /// <param name="str"></param>
        /// <param name="enumResult"></param>
        /// <param name="supportNumeric"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool TryToEnum<T>(string str, out T enumResult, bool supportNumeric = false) where T : struct, Enum
        {
            enumResult = default;
            var success = TryToEnum(str, typeof(T), out var result);
            if (success)
            {
                enumResult = (T)result;
            }

            return success;
        }

        /// <summary>
        /// 将字符串类型的数字转换为enum类型，支持KouEnumName标签特性别名枚举
        /// </summary>
        /// <param name="str"></param>
        /// <param name="enumType"></param>
        /// <param name="enumResult"></param>
        /// <param name="supportNumeric"></param>
        /// <returns></returns>
        public static bool TryToEnum(string str, Type enumType, out object enumResult, bool supportNumeric = false)
        {
            enumResult = null;
            if (str.IsNullOrEmpty()) return false;
            if (KouEnumTool.TryToKouEnum(enumType, str, out enumResult)) return true;
            try
            {
                if (!supportNumeric && str.IsInt()) return false;
                enumResult = Enum.Parse(enumType, str, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }
        #endregion

        #region 插件参数处理常用

        /// <summary>
        /// 转换为英文标点符号
        /// </summary>
        /// <returns></returns>
        public static string ToEnPunctuation(this string? str)
        {
            if (str.IsNullOrWhiteSpace()) return str;
            return str.ContainsAny(KouStaticData.ZhToEnPunctuationDict.Keys)
                ? str.ReplaceBasedOnDict(KouStaticData.ZhToEnPunctuationDict)
                : str;
        }

        /// <summary>
        /// 转换为中文标点符号
        /// </summary>
        /// <returns></returns>
        public static string ToZhPunctuation(this string? str)
        {
            if (str.IsNullOrWhiteSpace()) return str;
            return str.ContainsAny(KouStaticData.ZhToEnPunctuationDict.Values)
                ? str.ReplaceBasedOnDict(KouStaticData.ZhToEnPunctuationDict, true)
                : str;
        }
        #endregion

        #region 区间格式转区间

        /// <summary>
        /// 获取TimeSpan型区间值（格式为[7位天数.][00-23小时:][00-59分钟:]00-59秒[.7位毫秒数]）
        /// </summary>
        /// <param name="str"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool TryGetTimeSpanInterval(this string str, out TimeSpan left, out TimeSpan right)
        {
            left = new TimeSpan();
            right = new TimeSpan();
            if (str.IsNullOrWhiteSpace()) return false;
            str = str.Trim();
            //该正则表达式匹配[leftime]lday(7位天数).lhour(0-23):lminute(0-23):lsecond(0-59).lmillisecond(7位毫秒数) 分隔符 [righttime]rday(7位天数).rhour(0-23):rminute(0-23):rsecond(0-59).rmillisecond(7位毫秒数)
            var regex = new Regex(@"^(?<lefttime>(?:(?:(?<lday>\d{1,7})\.)?(?:(?<lhour>2[0-3]|[0-1]\d|\d):)?(?:(?<lminute>[0-5]\d|\d):))?(?<lsecond>[0-5]\d|\d)(?:(?:\.)?(?<lmillisecond>\d{1,7}))?)[^.:\d]+?(?<righttime>(?:(?:(?<rday>[0-5]\d|\d)\.)?(?:(?<rhour>2[0-3]|[0-1]\d|\d):)?(?:(?<rminute>[0-5]\d|\d):))?(?<rsecond>[0-5]\d|\d))(?:(?:\.)?(?<rmillisecond>\d{1,7}))?$");
            if (regex.IsMatch(str))
            {
                var groups = regex.Match(str).Groups;
                int.TryParse(groups["lday"]?.Value, out var lday);
                int.TryParse(groups["lhour"]?.Value, out var lhour);
                int.TryParse(groups["lminute"]?.Value, out var lminute);
                int.TryParse(groups["lsecond"]?.Value, out var lsecond);
                int.TryParse(groups["lmillisecond"]?.Value, out var lmillisecond);
                int.TryParse(groups["rday"]?.Value, out var rday);
                int.TryParse(groups["rhour"]?.Value, out var rhour);
                int.TryParse(groups["rminute"]?.Value, out var rminute);
                int.TryParse(groups["rsecond"]?.Value, out var rsecond);
                int.TryParse(groups["rmillisecond"]?.Value, out var rmillisecond);
                left = new TimeSpan(lday, lhour, lminute, lsecond, lmillisecond);
                right = new TimeSpan(rday, rhour, rminute, rsecond, rmillisecond);
                if (left <= right) return true;

            }
            return false;
        }

        #endregion

    }
}
