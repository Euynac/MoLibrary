namespace MoLibrary.AutoModel.Configurations;

public class AutoModelExpressionOptions
{
    public string SelectSeparator { get; set; } = ",";
    public string FilterMultiSeparator { get; set; } = ",";
    public string Fuzzy { get; set; } = "%";
    /// <summary>
    /// 表达式and
    /// </summary>
    public string And { get; set; } = "and";
    /// <summary>
    /// 表达式or
    /// </summary>
    public string Or { get; set; } = "or";

    public string ExpLikeAnd { get; set; } = "&";
    public string ExpLikeOr { get; set; } = "|";
    public string ExpLikeNot { get; set; } = "!";
}