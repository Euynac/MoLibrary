namespace Monica.AutoModel.Interfaces;

public interface IHasRequestFilter
{
    /// <summary>
    /// Dynamic filter expression.
    /// </summary>
    string? Filter { get; set; }
    /// <summary>
    /// Value used for fuzzy searching across multiple fields.
    /// </summary>
    string? Fuzzy { get; set; }
    /// <summary>
    /// Restricts fuzzy searching to specific fields.
    /// </summary>
    string? FuzzyColumns { get; set; }
}
