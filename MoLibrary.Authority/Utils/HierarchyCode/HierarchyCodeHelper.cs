using MoLibrary.Tool.Extensions;

namespace MoLibrary.Authority.Utils.HierarchyCode;

/// <summary>
/// Helper class for working with hierarchical codes
/// </summary>
public static class HierarchyCodeHelper
{
    /// <summary>
    /// Creates code for given numbers.
    /// Example: if numbers are 4,2 then returns "00004.00002";
    /// </summary>
    /// <param name="numbers">Numbers</param>
    public static string CreateCode(params int[] numbers)
    {
        if (numbers.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(numbers), "Given numbers can not be null or empty when create code.");
        }

        if (!HierarchyCodeOptions.UseRelativeDepth && numbers.Length > HierarchyCodeOptions.MaxDepth)
        {
            throw new ArgumentException($"Hierarchy depth {numbers.Length} exceeds maximum allowed depth of {HierarchyCodeOptions.MaxDepth}");
        }

        // If using relative depth and exceeding max depth, take only the last MaxDepth segments
        if (HierarchyCodeOptions.UseRelativeDepth && numbers.Length > HierarchyCodeOptions.MaxDepth)
        {
            numbers = numbers.Skip(numbers.Length - HierarchyCodeOptions.MaxDepth).ToArray();
        }

        return numbers.Select(number => number.ToString(new string('0', HierarchyCodeOptions.CodeLength))).JoinAsString(".");
    }

    /// <summary>
    /// Extracts the numbers from a hierarchical code.
    /// Example: if code is "00004.00002" then returns [4, 2]
    /// </summary>
    /// <param name="code">The hierarchical code to extract numbers from</param>
    /// <returns>List of integers representing each level in the hierarchy</returns>
    /// <exception cref="ArgumentNullException">Thrown when code is null or empty</exception>
    public static List<int> ExtractNumbers(string code)
    {
        if (code.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(code), "code can not be null or empty.");
        }

        var segments = code.Split('.');
        var numbers = new List<int>(segments.Length);

        foreach (var segment in segments)
        {
            if (int.TryParse(segment, out int number))
            {
                numbers.Add(number);
            }
            else
            {
                throw new FormatException($"Invalid code segment format: {segment}. Expected a numeric value.");
            }
        }

        return numbers;
    }

    /// <summary>
    /// Appends a child code to a parent code.
    /// Example: if parentCode = "00001", childCode = "00042" then returns "00001.00042".
    /// </summary>
    /// <param name="parentCode">Parent code. Can be null or empty if parent is a root.</param>
    /// <param name="childCode">Child code.</param>
    public static string AppendCode(string? parentCode, string childCode)
    {
        if (childCode.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(childCode), "childCode can not be null or empty.");
        }

        if (parentCode.IsNullOrEmptySet())
        {
            return childCode;
        }

        var result = parentCode + "." + childCode;
        
        // If using relative depth and exceeding max depth, remove oldest segments
        if (HierarchyCodeOptions.UseRelativeDepth)
        {
            var segments = result.Split('.');
            if (segments.Length > HierarchyCodeOptions.MaxDepth)
            {
                result = string.Join(".", segments.Skip(segments.Length - HierarchyCodeOptions.MaxDepth));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets relative code to the parent.
    /// Example: if code = "00019.00055.00001" and parentCode = "00019" then returns "00055.00001".
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="parentCode">The parent code.</param>
    public static string? GetRelativeCode(string code, string? parentCode)
    {
        if (code.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(code), "code can not be null or empty.");
        }

        if (parentCode.IsNullOrEmptySet())
        {
            return code;
        }

        if (code.Length == parentCode.Length)
        {
            return null;
        }

        return code[(parentCode.Length + 1)..];
    }

    /// <summary>
    /// Calculates next code for given code.
    /// Example: if code = "00019.00055.00001" returns "00019.00055.00002".
    /// </summary>
    /// <param name="code">The code.</param>
    public static string CalculateNextCode(string code)
    {
        if (code.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(code), "code can not be null or empty.");
        }

        var parentCode = GetParentCode(code);
        var lastUnitCode = GetLastCode(code);

        return AppendCode(parentCode, CreateCode(Convert.ToInt32(lastUnitCode) + 1));
    }

    /// <summary>
    /// Gets the last code segment.
    /// Example: if code = "00019.00055.00001" returns "00001".
    /// </summary>
    /// <param name="code">The code.</param>
    public static string GetLastCode(string code)
    {
        if (code.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(code), "code can not be null or empty.");
        }

        var splitCode = code.Split('.');
        return splitCode[^1];
    }

    /// <summary>
    /// Gets parent code.
    /// Example: if code = "00019.00055.00001" returns "00019.00055".
    /// </summary>
    /// <param name="code">The code.</param>
    public static string? GetParentCode(string code)
    {
        if (code.IsNullOrEmptySet())
        {
            throw new ArgumentNullException(nameof(code), "code can not be null or empty.");
        }

        var splitCode = code.Split('.');
        if (splitCode.Length == 1)
        {
            return null;
        }

        return splitCode.Take(splitCode.Length - 1).JoinAsString(".");
    }

    /// <summary>
    /// Gets the depth of the hierarchy code
    /// </summary>
    /// <param name="code">The hierarchy code</param>
    /// <returns>The number of segments in the code</returns>
    public static int GetDepth(string code)
    {
        if (string.IsNullOrEmpty(code))
            return 0;
        return code.Count(c => c == '.') + 1;
    }
} 