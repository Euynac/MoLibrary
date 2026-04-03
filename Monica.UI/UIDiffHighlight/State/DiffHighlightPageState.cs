using Monica.UI.UIDiffHighlight.Models;

namespace Monica.UI.UIDiffHighlight.State;

/// <summary>
/// Owns the mutable state of the diff highlight page.
/// </summary>
public sealed class DiffHighlightPageState
{
    private const string SampleOriginText = """
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }

    public double Divide(int a, int b)
    {
        return (double)a / b;
    }
}
""";

    private const string SampleNewText = """
public class Calculator
{
    public int Add(int a, int b)
    {
        // Add input validation
        return a + b;
    }

    public int Subtract(int a, int b)
    {
        return a - b;
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }

    public double Divide(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException("除数不能为零");
        return (double)a / b;
    }

    public double Power(double baseNum, double exponent)
    {
        return Math.Pow(baseNum, exponent);
    }
}
""";

    /// <summary>
    /// Gets the original text input.
    /// </summary>
    public string OriginText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the updated text input.
    /// </summary>
    public string NewText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the page should refresh automatically when text changes.
    /// </summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>
    /// Gets whether a refresh is currently running.
    /// </summary>
    public bool IsRefreshing { get; private set; }

    /// <summary>
    /// Gets the last non-HTML diff result rendered by the page.
    /// </summary>
    public DiffHighlightResult? DiffResult { get; private set; }

    /// <summary>
    /// Gets the current diff options.
    /// </summary>
    public DiffHighlightOptions Options { get; } = new()
    {
        Mode = EDiffHighlightMode.Character,
        OutputFormat = EDiffOutputFormat.Html,
        ContextLines = 3,
        MaxCharacterDiffLength = 1000,
        IgnoreWhitespace = false,
        IgnoreCase = false
    };

    /// <summary>
    /// Updates the original text input.
    /// </summary>
    public void SetOriginText(string value)
    {
        OriginText = value;
    }

    /// <summary>
    /// Updates the new text input.
    /// </summary>
    public void SetNewText(string value)
    {
        NewText = value;
    }

    /// <summary>
    /// Replaces the current content with sample data.
    /// </summary>
    public void LoadSampleData()
    {
        OriginText = SampleOriginText;
        NewText = SampleNewText;
        DiffResult = null;
    }

    /// <summary>
    /// Marks the page as refreshing.
    /// </summary>
    public void BeginRefresh()
    {
        IsRefreshing = true;
    }

    /// <summary>
    /// Completes the current refresh and stores the latest non-HTML result.
    /// </summary>
    public void CompleteRefresh(DiffHighlightResult? diffResult)
    {
        DiffResult = diffResult;
        IsRefreshing = false;
    }

    /// <summary>
    /// Returns the combined character count of both text inputs.
    /// </summary>
    public int GetTotalCharacterCount()
    {
        return OriginText.Length + NewText.Length;
    }
}
