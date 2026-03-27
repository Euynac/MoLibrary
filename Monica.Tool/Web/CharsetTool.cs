using System.Text;
using System.Text.RegularExpressions;

namespace Monica.Tool.Web;

/// <summary>
/// Character set tools
/// </summary>
public class CharsetTool
{
    /// <summary>
    /// Convert string to Unicode
    /// </summary>
    /// <param name="source">source string</param>
    /// <returns>Unicode encoded string</returns>
    public static string String2Unicode(string source)
    {
        var bytes = Encoding.Unicode.GetBytes(source);
        var stringBuilder = new StringBuilder();
        for (var i = 0; i < bytes.Length; i += 2)
        {
            stringBuilder.AppendFormat("\\u{0}{1}", bytes[i + 1].ToString("x").PadLeft(2, '0'), bytes[i].ToString("x").PadLeft(2, '0'));
        }
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Convert Unicode escape text to a regular string
    /// </summary>
    /// <param name="source">Unicode encoded string</param>
    /// <returns>normal string</returns>
    public static string Unicode2String(string source)
    {
        return new Regex(@"\\u([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled).Replace(
            source, x => string.Empty + Convert.ToChar(Convert.ToUInt16(x.Result("$1"), 16)));
    }
}
