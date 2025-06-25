namespace MoLibrary.Repository.Attributes;


[AttributeUsage( AttributeTargets.Property)]
public class IgnoreUpdateAttribute : Attribute
{
    public const string FEATURE_KEY = "IgnoreUpdate_Feature";
}