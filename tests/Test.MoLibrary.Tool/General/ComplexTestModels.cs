namespace Test.MoLibrary.Tool.General;

/// <summary>
/// Self-referencing node for circular reference testing.
/// </summary>
public class SelfReferencingNode
{
    public string? Name { get; set; }
    public SelfReferencingNode? Parent { get; set; }
    public List<SelfReferencingNode>? Children { get; set; }
}

/// <summary>
/// Person A for mutual reference testing (A -> B -> A).
/// </summary>
public class PersonA
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public PersonB? Friend { get; set; }
}

/// <summary>
/// Person B for mutual reference testing (B -> A -> B).
/// </summary>
public class PersonB
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public PersonA? Friend { get; set; }
}

/// <summary>
/// Node for triangular circular reference testing (A -> B -> C -> A).
/// </summary>
public class TriangleNodeA
{
    public string? Name { get; set; }
    public TriangleNodeB? Next { get; set; }
}

public class TriangleNodeB
{
    public string? Name { get; set; }
    public TriangleNodeC? Next { get; set; }
}

public class TriangleNodeC
{
    public string? Name { get; set; }
    public TriangleNodeA? Next { get; set; }
}

/// <summary>
/// Deeply nested class for depth testing.
/// </summary>
public class DeepNest
{
    public string? Level { get; set; }
    public int Depth { get; set; }
    public DeepNest? Inner { get; set; }

    public static DeepNest CreateNested(int depth)
    {
        var root = new DeepNest { Level = "Level_0", Depth = 0 };
        var current = root;
        for (var i = 1; i < depth; i++)
        {
            current.Inner = new DeepNest { Level = $"Level_{i}", Depth = i };
            current = current.Inner;
        }
        return root;
    }
}

/// <summary>
/// Complex object with many properties for performance testing.
/// </summary>
public class ComplexObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public decimal Price { get; set; }
    public double Rating { get; set; }
    public float Score { get; set; }
    public long ViewCount { get; set; }
    public Guid UniqueId { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
    public NestedObject? Nested { get; set; }
    public List<NestedObject>? NestedList { get; set; }
    public ComplexStatus Status { get; set; }
    public byte[]? Data { get; set; }
    public TimeSpan Duration { get; set; }
    public Uri? Url { get; set; }
    public Version? Version { get; set; }
}

public class NestedObject
{
    public int Value { get; set; }
    public string? Text { get; set; }
    public NestedObject? Child { get; set; }
}

public enum ComplexStatus
{
    None,
    Pending,
    Active,
    Completed,
    Cancelled
}

/// <summary>
/// Object that holds multiple references to the same instance.
/// </summary>
public class RepeatedReferenceContainer
{
    public SharedObject? First { get; set; }
    public SharedObject? Second { get; set; }
    public SharedObject? Third { get; set; }
    public List<SharedObject?>? SharedList { get; set; }
}

public class SharedObject
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Object with mixed types including special types.
/// </summary>
public class MixedTypeContainer
{
    public Type? TypeProperty { get; set; }
    public Exception? ExceptionProperty { get; set; }
    public Delegate? DelegateProperty { get; set; }
    public object? ObjectProperty { get; set; }
    public List<object?>? MixedList { get; set; }
}

/// <summary>
/// Self-referencing list for testing list that contains itself.
/// </summary>
public class SelfReferencingList : List<object?>
{
    public string? Name { get; set; }
}

/// <summary>
/// Object with a property that throws exception when accessed.
/// </summary>
public class ObjectWithThrowingProperty
{
    public string? Name { get; set; }

    public string ThrowingProperty => throw new InvalidOperationException("This property always throws!");

    public string? SafeProperty { get; set; }
}

/// <summary>
/// Chinese unicode test object.
/// </summary>
public class UnicodeTestObject
{
    public string? ChineseName { get; set; }
    public string? JapaneseName { get; set; }
    public string? EmojiField { get; set; }
    public string? SpecialChars { get; set; }
}

/// <summary>
/// Large object for performance testing with many properties.
/// </summary>
public class LargePropertyObject
{
    public string? Prop001 { get; set; }
    public string? Prop002 { get; set; }
    public string? Prop003 { get; set; }
    public string? Prop004 { get; set; }
    public string? Prop005 { get; set; }
    public string? Prop006 { get; set; }
    public string? Prop007 { get; set; }
    public string? Prop008 { get; set; }
    public string? Prop009 { get; set; }
    public string? Prop010 { get; set; }
    public string? Prop011 { get; set; }
    public string? Prop012 { get; set; }
    public string? Prop013 { get; set; }
    public string? Prop014 { get; set; }
    public string? Prop015 { get; set; }
    public string? Prop016 { get; set; }
    public string? Prop017 { get; set; }
    public string? Prop018 { get; set; }
    public string? Prop019 { get; set; }
    public string? Prop020 { get; set; }
    public int Int001 { get; set; }
    public int Int002 { get; set; }
    public int Int003 { get; set; }
    public int Int004 { get; set; }
    public int Int005 { get; set; }
    public double Double001 { get; set; }
    public double Double002 { get; set; }
    public double Double003 { get; set; }
    public double Double004 { get; set; }
    public double Double005 { get; set; }
    public DateTime? Date001 { get; set; }
    public DateTime? Date002 { get; set; }
    public DateTime? Date003 { get; set; }
    public DateTime? Date004 { get; set; }
    public DateTime? Date005 { get; set; }
    public List<string>? List001 { get; set; }
    public List<string>? List002 { get; set; }
    public List<string>? List003 { get; set; }
    public Dictionary<string, int>? Dict001 { get; set; }
    public Dictionary<string, int>? Dict002 { get; set; }

    public static LargePropertyObject CreatePopulated()
    {
        return new LargePropertyObject
        {
            Prop001 = "Value001",
            Prop002 = "Value002",
            Prop003 = "Value003",
            Prop004 = "Value004",
            Prop005 = "Value005",
            Prop006 = "Value006",
            Prop007 = "Value007",
            Prop008 = "Value008",
            Prop009 = "Value009",
            Prop010 = "Value010",
            Prop011 = "Value011",
            Prop012 = "Value012",
            Prop013 = "Value013",
            Prop014 = "Value014",
            Prop015 = "Value015",
            Prop016 = "Value016",
            Prop017 = "Value017",
            Prop018 = "Value018",
            Prop019 = "Value019",
            Prop020 = "Value020",
            Int001 = 1,
            Int002 = 2,
            Int003 = 3,
            Int004 = 4,
            Int005 = 5,
            Double001 = 1.1,
            Double002 = 2.2,
            Double003 = 3.3,
            Double004 = 4.4,
            Double005 = 5.5,
            Date001 = DateTime.Now,
            Date002 = DateTime.Now.AddDays(1),
            Date003 = DateTime.Now.AddDays(2),
            Date004 = DateTime.Now.AddDays(3),
            Date005 = DateTime.Now.AddDays(4),
            List001 = ["a", "b", "c"],
            List002 = ["d", "e", "f"],
            List003 = ["g", "h", "i"],
            Dict001 = new Dictionary<string, int> { ["key1"] = 1, ["key2"] = 2 },
            Dict002 = new Dictionary<string, int> { ["key3"] = 3, ["key4"] = 4 }
        };
    }
}
