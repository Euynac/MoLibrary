namespace MoLibrary.Repository.Attributes;

/// <summary>
/// 指示当前字段在Update语句时直接忽略Update
/// </summary>

[AttributeUsage( AttributeTargets.Property)]
public class IgnoreUpdateAttribute : Attribute
{
    public const string FEATURE_KEY = "IgnoreUpdate_Feature";
}