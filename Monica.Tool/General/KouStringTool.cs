using System.Text.RegularExpressions;
using Monica.Tool.Extensions;

namespace Monica.Tool.General;

/// <summary>
/// Kou develops common string tool classes
/// </summary>
public static class KouStringTool
{
    #region KouType类型适配
    /// <summary>
    /// Convert string type numbers to bool type, supports Chinese, English, and numbers
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
    /// Convert string type numbers to enum type, support KouEnumName label specific alias enumeration
    /// </summary>
    /// <param name="str"></param>
    /// <param name="enumResult"></param>
    /// <param name="supportNumeric"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool TryToEnum<T>(string str, out T enumResult, bool supportNumeric = false) where T : struct, Enum
    {
        enumResult = default;
        if (TryToEnum(str, typeof(T), out var result, supportNumeric) && result is T typedResult)
        {
            enumResult = typedResult;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Convert string type numbers to enum type, support KouEnumName label specific alias enumeration
    /// </summary>
    /// <param name="str"></param>
    /// <param name="enumType"></param>
    /// <param name="enumResult"></param>
    /// <param name="supportNumeric"></param>
    /// <returns></returns>
    public static bool TryToEnum(string str, Type enumType, out object? enumResult, bool supportNumeric = false)
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
    /// Convert to English punctuation marks
    /// </summary>
    /// <returns></returns>
    public static string ToEnPunctuation(this string str)
    {
        if (str.IsNullOrWhiteSpace()) return str;
        return str.ContainsAny(KouStaticData.ZhToEnPunctuationDict.Keys)
            ? str.ReplaceBasedOnDict(KouStaticData.ZhToEnPunctuationDict)
            : str;
    }

    /// <summary>
    /// Convert to Chinese punctuation marks
    /// </summary>
    /// <returns></returns>
    public static string ToZhPunctuation(this string str)
    {
        if (str.IsNullOrWhiteSpace()) return str;
        return str.ContainsAny(KouStaticData.ZhToEnPunctuationDict.Values)
            ? str.ReplaceBasedOnDict(KouStaticData.ZhToEnPunctuationDict, true)
            : str;
    }
    #endregion

    #region 区间格式转区间

    /// <summary>
    /// Get the TimeSpan interval value (the format is [7-digit days.][00-23 hours:][00-59 minutes:]00-59 seconds[.7-digit milliseconds])
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
        //This regular expression matches [leftime]lday(7-digit number of days).lhour(0-23):lminute(0-23):lsecond(0-59).lmillisecond(7-digit number of milliseconds) delimiter [righttime]rday(7-digit number of days).rhour(0-23):rminute(0-23):rsecond(0-59).rmillisecond(7-digit number of milliseconds)
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
