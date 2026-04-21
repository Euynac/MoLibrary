namespace Monica.Utilities.Text.Models;

/// <summary>
/// Identifies the text or JSON transformation to execute.
/// </summary>
public enum TextTransformOperation
{
    /// <summary>
    /// Decodes JSON-style escaped text such as <c>\u4e2d\u6587</c> into readable characters.
    /// </summary>
    DecodeEscapedText,

    /// <summary>
    /// Parses JSON and rewrites it with indentation while preserving readable Unicode characters.
    /// </summary>
    FormatJson,

    /// <summary>
    /// Parses JSON and rewrites it in a compact form while preserving readable Unicode characters.
    /// </summary>
    MinifyJson
}

/// <summary>
/// Describes a text transformation request.
/// </summary>
public sealed record TextTransformRequest(string Input, TextTransformOperation Operation)
{
    /// <summary>
    /// Returns a normalized request whose input has been validated and trimmed only where that is required by the operation.
    /// </summary>
    /// <returns>A validated request ready for processing.</returns>
    public TextTransformRequest Normalize()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            throw new ArgumentException("Input text is required.", nameof(Input));
        }

        return this with
        {
            Input = Operation is TextTransformOperation.FormatJson or TextTransformOperation.MinifyJson
                ? Input.Trim()
                : Input
        };
    }
}

/// <summary>
/// Represents the output of a text or JSON transformation.
/// </summary>
public sealed record TextTransformResult(
    TextTransformOperation Operation,
    string Output,
    int InputLength,
    int OutputLength,
    int DecodePasses,
    int JsonStringLayersRemoved,
    string Summary);
