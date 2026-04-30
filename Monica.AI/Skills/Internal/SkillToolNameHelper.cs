using System.Text;

namespace Monica.AI.Skills.Internal;

internal static class SkillToolNameHelper
{
    internal static string DeriveName(string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        var source = memberName.EndsWith("Async", StringComparison.Ordinal)
            ? memberName[..^"Async".Length]
            : memberName;

        var builder = new StringBuilder(source.Length + 8);
        var previousWasHyphen = false;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (char.IsLetterOrDigit(c))
            {
                if (builder.Length > 0
                    && !previousWasHyphen
                    && char.IsUpper(c)
                    && IsWordBoundary(source, i))
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
                previousWasHyphen = false;
                continue;
            }

            if (builder.Length > 0 && !previousWasHyphen)
            {
                builder.Append('-');
                previousWasHyphen = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0
            ? throw new InvalidOperationException($"Cannot derive an AI tool name from member '{memberName}'.")
            : builder.ToString();
    }

    private static bool IsWordBoundary(string source, int index)
    {
        if (index == 0)
        {
            return false;
        }

        var previous = source[index - 1];
        if (!char.IsLetterOrDigit(previous))
        {
            return false;
        }

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        return index + 1 < source.Length && char.IsLower(source[index + 1]);
    }
}
