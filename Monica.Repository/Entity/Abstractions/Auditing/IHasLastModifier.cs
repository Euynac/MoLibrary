namespace Monica.Repository.Entity.Abstractions.Auditing;

public interface IHasLastModifier
{
    public string? LastModifierId { get; }
}

public interface IHasLastModifierName
{
    public string? LastModifier { get; set; }
}

