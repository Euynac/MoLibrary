namespace Monica.Configuration.Models;

public enum ConfigurationValueKind
{
    String,
    Numeric,
    DateTime,
    TimeSpan,
    Boolean,
    Enum,
    Object
}

public enum ConfigurationSpecialValueKind
{
    Array,
    Dict
}
