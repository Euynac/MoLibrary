namespace MoLibrary.Repository.EntityInterfaces.Auditing;

/// <summary>
/// Standard interface for an entity that MAY have a creator.
/// </summary>
public interface IHasCreator
{
    /// <summary>
    /// ID of the creator.
    /// </summary>
    string? CreatorId { get; }
}

public interface IHasCreatorName
{
    public string? Creator { get; set; }
}