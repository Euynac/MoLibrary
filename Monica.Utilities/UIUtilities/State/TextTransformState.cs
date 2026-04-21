using Monica.Core.Results;
using Monica.Utilities.Text.Facades;
using Monica.Utilities.Text.Models;

namespace Monica.Utilities.UIUtilities.State;

/// <summary>
/// Owns the transient UI state for the text and JSON transformation panel.
/// </summary>
public sealed class TextTransformState(TextTransformFacade textTransformFacade)
{
    /// <summary>
    /// Gets or sets the current input text.
    /// </summary>
    public string InputText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the latest transformation output.
    /// </summary>
    public string OutputText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the latest transformation error, if any.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the latest successful transformation result.
    /// </summary>
    public TextTransformResult? LastResult { get; private set; }

    /// <summary>
    /// Executes the selected text transformation.
    /// </summary>
    /// <param name="operation">Transformation operation to execute.</param>
    /// <returns>The result envelope returned by the facade.</returns>
    public Res<TextTransformResult> Execute(TextTransformOperation operation)
    {
        ErrorMessage = null;

        var result = textTransformFacade.Transform(new TextTransformRequest(InputText, operation));
        if (result.IsFailed(out var error, out var transformed))
        {
            ErrorMessage = error.Message;
        }
        else
        {
            LastResult = transformed;
            OutputText = transformed.Output;
        }

        return result;
    }

    /// <summary>
    /// Replaces the current input with the latest output so the next action can build on it.
    /// </summary>
    public void UseOutputAsInput()
    {
        if (string.IsNullOrEmpty(OutputText))
        {
            return;
        }

        InputText = OutputText;
        ErrorMessage = null;
    }

    /// <summary>
    /// Clears the input, output, and result metadata.
    /// </summary>
    public void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        ErrorMessage = null;
        LastResult = null;
    }
}
