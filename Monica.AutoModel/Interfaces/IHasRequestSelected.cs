namespace Monica.AutoModel.Interfaces;

public interface IHasRequestSelect
{
    /// <summary>
    /// Response field selection that includes only specific fields.
    /// </summary>
    string? SelectColumns { get; set; }

    /// <summary>
    /// Response field selection that excludes the specified fields. Cannot be used with <see cref="SelectColumns"/>.
    /// </summary>
    string? SelectExceptColumns { get; set; }

    /// <summary>
    /// Indicates whether any field selection has been applied.
    /// </summary>

    bool HasUsingSelected()
    {
        return !string.IsNullOrWhiteSpace(SelectColumns) || !string.IsNullOrWhiteSpace(SelectExceptColumns);
    }
}
