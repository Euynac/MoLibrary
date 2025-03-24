namespace MoLibrary.Repository.EntityInterfaces.Auditing;

public interface IHasLastModifier
{
    public string? LastModifierId { get; }
}

public interface IHasLastModifierName
{
    public string? LastModifier { get; set; }
}

