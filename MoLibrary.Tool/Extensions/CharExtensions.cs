namespace MoLibrary.Tool.Extensions;

public static class CharExtensions
{
    /// <summary>Indicates whether the specified Unicode character is categorized as an uppercase letter.</summary>
    /// <param name="c">The Unicode character to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="c" /> is an uppercase letter; otherwise, <see langword="false" />.</returns>
    public static bool IsUpper(this char c) => char.IsUpper(c);
    /// <summary>Indicates whether the specified Unicode character is categorized as a lowercase letter.</summary>
    /// <param name="c">The Unicode character to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="c" /> is a lowercase letter; otherwise, <see langword="false" />.</returns>
    public static bool IsLower(this char c) => char.IsLower(c);

    /// <summary>Indicates whether the specified Unicode character is categorized as a decimal digit.</summary>
    /// <param name="c">The Unicode character to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="c" /> is a decimal digit; otherwise, <see langword="false" />.</returns>
    public static bool IsDigit(this char c) => char.IsDigit(c);
    /// <summary>
    /// Convert char[] to string. Actually is new string(char[]).
    /// </summary>
    /// <param name="chars"></param>
    /// <returns></returns>
    public static string ConvertToString(this char[] chars) => new(chars);
}