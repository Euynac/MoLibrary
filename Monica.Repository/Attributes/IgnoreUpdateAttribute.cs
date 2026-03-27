namespace Monica.Repository.Attributes;

/// <summary>
/// Instructs the current field to directly ignore Update when updating the statement.
/// </summary>

[AttributeUsage( AttributeTargets.Property)]
public class IgnoreUpdateAttribute : Attribute
{
    public const string FEATURE_KEY = "IgnoreUpdate_Feature";
}