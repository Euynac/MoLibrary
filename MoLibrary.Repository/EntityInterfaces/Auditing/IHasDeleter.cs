namespace MoLibrary.Repository.EntityInterfaces.Auditing;

/// <summary>
/// Standard interface for an entity that MAY have a deleter.
/// </summary>
public interface IHasDeleter
{
    /// <summary>
    /// Gets the identifier of the deleter.
    /// </summary>
    string? DeleterId { get; }
}
/// <summary>
/// Interface for an entity that may have a deleter name.
/// </summary>
public interface IHasDeleterName
{
    /// <summary>
    /// Gets or sets the name of the deleter.
    /// </summary>
    public string? Deleter { get; set; }
}
