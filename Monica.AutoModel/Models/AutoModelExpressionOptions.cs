namespace Monica.AutoModel.Models;

public class AutoModelExpressionOptions
{
    public string SelectSeparator { get; set; } = ",";
    public string FilterMultiSeparator { get; set; } = ",";
    public string Fuzzy { get; set; } = "%";
    /// <summary>
    /// Logical AND token used in expressions.
    /// </summary>
    public string And { get; set; } = "and";
    /// <summary>
    /// Logical OR token used in expressions.
    /// </summary>
    public string Or { get; set; } = "or";

    public string ExpLikeAnd { get; set; } = "&";
    public string ExpLikeOr { get; set; } = "|";
    public string ExpLikeNot { get; set; } = "!";
}
