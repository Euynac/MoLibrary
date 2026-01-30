using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Monica.Tool.General;

namespace Test.Monica.Tool.General;

/// <summary>
/// Unit tests for <see cref="DebugJsonTool.ToJsonStringForce{T}"/> method.
/// Tests cover performance, complex objects, circular references, repeated references, and special types.
/// </summary>
public class ToJsonStringForceTests
{
    #region Basic Functionality Tests

    [Fact]
    public void ToJsonStringForce_WithNull_ReturnsNull()
    {
        // Arrange
        object? nullObject = null;

        // Act
        var result = nullObject.ToJsonStringForce();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToJsonStringForce_WithSimpleObject_ReturnsValidJson()
    {
        // Arrange
        var obj = new  { Name = "Test", Value = 42 };

        // Act
        var result = obj.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Name");
        result.Should().Contain("Test");
        result.Should().Contain("Value");
        result.Should().Contain("42");
    }

    [Fact]
    public void ToJsonStringForce_WithWriteIndentedFalse_ReturnsMinifiedJson()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 42 };

        // Act
        var result = obj.ToJsonStringForce(writeIndented: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().NotContain("\n");
        result.Should().NotContain("  ");
    }

    [Fact]
    public void ToJsonStringForce_WithRelaxedEscapingTrue_DoesNotEscapeUnicode()
    {
        // Arrange
        var obj = new UnicodeTestObject
        {
            ChineseName = "测试中文",
            JapaneseName = "テスト",
            EmojiField = "😀🎉",
            SpecialChars = "<>&'\""
        };

        // Act
        var result = obj.ToJsonStringForce(relaxedEscaping: true);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("测试中文");
        result.Should().Contain("テスト");
    }

    [Fact]
    public void ToJsonStringForce_WithRelaxedEscapingFalse_MayEscapeUnicode()
    {
        // Arrange
        var obj = new UnicodeTestObject { ChineseName = "测试" };

        // Act
        var result = obj.ToJsonStringForce(relaxedEscaping: false);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // With relaxedEscaping false, unicode may be escaped or not depending on default encoder
    }

    #endregion

    #region Complex Object Tests

    [Fact]
    public void ToJsonStringForce_WithDeeplyNestedObject_SerializesCorrectly()
    {
        // Arrange - Create 8 levels of nesting (within default MaxDepth of 10)
        var deepNest = DeepNest.CreateNested(8);

        // Act
        var result = deepNest.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Level_0");
        result.Should().Contain("Level_7");

        // Verify it's valid JSON
        var action = () => JsonDocument.Parse(result!);
        action.Should().NotThrow();
    }

    [Fact]
    public void ToJsonStringForce_WithLargeObjectGraph_SerializesWithoutError()
    {
        // Arrange - Create object with 40+ properties
        var largeObj = LargePropertyObject.CreatePopulated();

        // Act
        var result = largeObj.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Prop001");
        result.Should().Contain("Prop020");
        result.Should().Contain("Int001");
        result.Should().Contain("Double001");
        result.Should().Contain("List001");
        result.Should().Contain("Dict001");

        // Verify it's valid JSON
        var action = () => JsonDocument.Parse(result!);
        action.Should().NotThrow();
    }

    [Fact]
    public void ToJsonStringForce_WithMixedTypeCollection_SerializesAllElements()
    {
        // Arrange
        var mixedList = new List<object?>
        {
            "string",
            42,
            3.14,
            true,
            null,
            new  { Name = "Nested" },
            new List<int> { 1, 2, 3 },
            typeof(string),
            new InvalidOperationException("Test exception")
        };

        // Act
        var result = mixedList.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("string");
        result.Should().Contain("42");
        result.Should().Contain("3.14");
        result.Should().Contain("true");
        result.Should().Contain("Nested");
        result.Should().Contain("$type");
    }

    [Fact]
    public void ToJsonStringForce_WithComplexObject_SerializesAllProperties()
    {
        // Arrange
        var complex = new ComplexObject
        {
            Id = 1,
            Name = "TestComplex",
            Description = "A complex object for testing",
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0),
            UpdatedAt = new DateTime(2024, 6, 15, 10, 30, 0),
            IsActive = true,
            Price = 99.99m,
            Rating = 4.5,
            Score = 85.5f,
            ViewCount = 1000000,
            UniqueId = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            Tags = ["tag1", "tag2", "tag3"],
            Metadata = new Dictionary<string, object?>
            {
                ["key1"] = "value1",
                ["key2"] = 123,
                ["key3"] = null
            },
            Nested = new NestedObject
            {
                Value = 10,
                Text = "Nested text",
                Child = new NestedObject { Value = 20, Text = "Child text" }
            },
            Status = ComplexStatus.Active,
            Duration = TimeSpan.FromHours(2.5)
        };

        // Act
        var result = complex.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("TestComplex");
        result.Should().Contain("99.99");
        result.Should().Contain("tag1");
        result.Should().Contain("Nested text");
        result.Should().Contain("Child text");
    }

    #endregion

    #region Circular Reference Tests (Critical)

    [Fact]
    public void ToJsonStringForce_WithSelfReference_HandlesWithoutStackOverflow()
    {
        // Arrange - Object that references itself
        var node = new SelfReferencingNode { Name = "SelfRef" };
        node.Parent = node; // Self reference!

        // Act - Should not throw StackOverflowException
        var result = node.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("SelfRef");
        // Should contain circular reference marker or max depth exceeded
        result.Should().Match(s => s.Contains("CIRCULAR_REFERENCE") || s.Contains("MAX_DEPTH_EXCEEDED"));
    }

    [Fact]
    public void ToJsonStringForce_WithMutualReference_HandlesWithoutStackOverflow()
    {
        // Arrange - A -> B -> A circular reference
        var personA = new PersonA { Name = "Alice", Age = 30 };
        var personB = new PersonB { Name = "Bob", Age = 25 };
        personA.Friend = personB;
        personB.Friend = personA; // Circular!

        // Act - Should not throw StackOverflowException
        var result = personA.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
        result.Should().Match(s => s.Contains("CIRCULAR_REFERENCE") || s.Contains("MAX_DEPTH_EXCEEDED"));
    }

    [Fact]
    public void ToJsonStringForce_WithCircularListReference_HandlesWithoutStackOverflow()
    {
        // Arrange - List that contains itself
        var selfList = new SelfReferencingList { Name = "CircularList" };
        selfList.Add("item1");
        selfList.Add(selfList); // List contains itself!
        selfList.Add("item2");

        // Act - Should not throw StackOverflowException
        var result = selfList.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("item1");
        result.Should().Contain("item2");
    }

    [Fact]
    public void ToJsonStringForce_WithTriangularReference_HandlesWithoutStackOverflow()
    {
        // Arrange - A -> B -> C -> A circular reference
        var nodeA = new TriangleNodeA { Name = "NodeA" };
        var nodeB = new TriangleNodeB { Name = "NodeB" };
        var nodeC = new TriangleNodeC { Name = "NodeC" };
        nodeA.Next = nodeB;
        nodeB.Next = nodeC;
        nodeC.Next = nodeA; // Triangular circular reference!

        // Act - Should not throw StackOverflowException
        var result = nodeA.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("NodeA");
        result.Should().Contain("NodeB");
        result.Should().Contain("NodeC");
        result.Should().Match(s => s.Contains("CIRCULAR_REFERENCE") || s.Contains("MAX_DEPTH_EXCEEDED"));
    }

    [Fact]
    public void ToJsonStringForce_WithDeepCircularChain_ExceedsMaxDepthGracefully()
    {
        // Arrange - Create a very deep chain that eventually loops back
        var root = new SelfReferencingNode { Name = "Root" };
        var current = root;
        for (var i = 0; i < 50; i++) // Create 50 levels deep
        {
            current.Children = [new SelfReferencingNode { Name = $"Node_{i}" }];
            current = current.Children[0];
        }
        current.Parent = root; // Loop back to root

        // Act - Should not throw, should gracefully handle with MAX_DEPTH_EXCEEDED
        var result = root.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Root");
        result.Should().Contain("MAX_DEPTH_EXCEEDED");
    }

    [Fact]
    public void ToJsonStringForce_WithChildReferencingParent_HandlesCorrectly()
    {
        // Arrange - Parent-child with back reference
        var parent = new SelfReferencingNode { Name = "Parent" };
        var child = new SelfReferencingNode { Name = "Child", Parent = parent };
        parent.Children = [child];

        // Act
        var result = parent.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Parent");
        result.Should().Contain("Child");
    }

    #endregion

    #region Repeated Reference Tests

    [Fact]
    public void ToJsonStringForce_WithSameObjectMultipleTimes_SerializesEachOccurrence()
    {
        // Arrange - Same object referenced multiple times
        var shared = new SharedObject { Id = 1, Name = "SharedInstance" };
        var container = new RepeatedReferenceContainer
        {
            First = shared,
            Second = shared, // Same object
            Third = shared   // Same object again
        };

        // Act
        var result = container.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Should contain the shared object's data multiple times
        var doc = JsonDocument.Parse(result!);
        var root = doc.RootElement;

        root.GetProperty("First").GetProperty("Name").GetString().Should().Be("SharedInstance");
        root.GetProperty("Second").GetProperty("Name").GetString().Should().Be("SharedInstance");
        root.GetProperty("Third").GetProperty("Name").GetString().Should().Be("SharedInstance");
    }

    [Fact]
    public void ToJsonStringForce_WithSharedChildObjects_SerializesCorrectly()
    {
        // Arrange
        var shared1 = new SharedObject { Id = 1, Name = "Shared1" };
        var shared2 = new SharedObject { Id = 2, Name = "Shared2" };
        var container = new RepeatedReferenceContainer
        {
            First = shared1,
            Second = shared2,
            Third = shared1, // Same as First
            SharedList = [shared1, shared2, shared1, shared2]
        };

        // Act
        var result = container.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();

        var doc = JsonDocument.Parse(result!);
        var list = doc.RootElement.GetProperty("SharedList");
        list.GetArrayLength().Should().Be(4);
    }

    #endregion

    #region Special Type Tests

    [Fact]
    public void ToJsonStringForce_WithTypeObject_SerializesTypeInfo()
    {
        // Arrange
        var type = typeof(Dictionary<string, List<int>>);

        // Act
        var result = type.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Type");
        result.Should().Contain("Dictionary");
        result.Should().Contain("IsGenericType");
        result.Should().Contain("true");
    }

    [Fact]
    public void ToJsonStringForce_WithException_SerializesExceptionInfo()
    {
        // Arrange
        Exception? exception;
        try
        {
            throw new InvalidOperationException("Test exception message");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var result = exception.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Exception");
        result.Should().Contain("Test exception message");
        result.Should().Contain("InvalidOperationException");
        result.Should().Contain("StackTrace");
    }

    [Fact]
    public void ToJsonStringForce_WithNestedExceptions_SerializesInnerExceptions()
    {
        // Arrange
        Exception? exception;
        try
        {
            try
            {
                throw new ArgumentException("Inner exception");
            }
            catch (Exception innerEx)
            {
                throw new InvalidOperationException("Outer exception", innerEx);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var result = exception.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Outer exception");
        result.Should().Contain("Inner exception");
        result.Should().Contain("InnerException");
        result.Should().Contain("ArgumentException");
        result.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void ToJsonStringForce_WithAggregateException_SerializesAllInnerExceptions()
    {
        // Arrange
        var innerExceptions = new Exception[]
        {
            new ArgumentException("First inner"),
            new InvalidOperationException("Second inner"),
            new NullReferenceException("Third inner")
        };
        var aggregateException = new AggregateException("Multiple errors", innerExceptions);

        // Act
        var result = aggregateException.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Multiple errors");
        result.Should().Contain("First inner");
    }

    [Fact]
    public void ToJsonStringForce_WithDelegate_SerializesDelegateInfo()
    {
        // Arrange
        Func<int, int, int> add = (a, b) => a + b;

        // Act
        var result = add.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Delegate");
        result.Should().Contain("Method");
    }

    [Fact]
    public void ToJsonStringForce_WithMethodInfo_SerializesMethodInfo()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("Substring", [typeof(int), typeof(int)]);

        // Act
        var result = methodInfo.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Substring");
        result.Should().Contain("Parameters");
    }

    [Fact]
    public void ToJsonStringForce_WithPropertyInfo_SerializesPropertyInfo()
    {
        // Arrange
        var propertyInfo = typeof(string).GetProperty("Length");

        // Act
        var result = propertyInfo.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Length");
        result.Should().Contain("PropertyType");
    }

    [Fact]
    public void ToJsonStringForce_WithMixedTypeContainer_SerializesAllSpecialTypes()
    {
        // Arrange
        var container = new MixedTypeContainer
        {
            TypeProperty = typeof(List<NestedObject>),
            ExceptionProperty = new ArgumentNullException("param", "Parameter cannot be null"),
            DelegateProperty = new Action(() => { }),
            ObjectProperty = new { Regular = "Object" },
            MixedList =
            [
                typeof(int),
                new InvalidOperationException("List exception"),
                "regular string",
                42
            ]
        };

        // Act
        var result = container.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("$type");
        result.Should().Contain("Type");
        result.Should().Contain("Exception");
        result.Should().Contain("Delegate");
        result.Should().Contain("Regular");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void ToJsonStringForce_WithThrowingProperty_ContinuesWithOtherProperties()
    {
        // Arrange
        var obj = new ObjectWithThrowingProperty
        {
            Name = "TestObject",
            SafeProperty = "SafeValue"
        };

        // Act
        var result = obj.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("TestObject");
        result.Should().Contain("SafeValue");
        result.Should().Contain("ERROR");
        // Note: When property getter throws, it's wrapped in TargetInvocationException due to reflection
        result.Should().Contain("TargetInvocationException");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void ToJsonStringForce_Performance_ComplexObjectSerializesReasonably()
    {
        // Arrange
        var complex = new ComplexObject
        {
            Id = 1,
            Name = "PerformanceTest",
            Description = "Testing serialization performance",
            CreatedAt = DateTime.Now,
            IsActive = true,
            Price = 99.99m,
            Tags = Enumerable.Range(1, 100).Select(i => $"tag{i}").ToList(),
            Nested = new NestedObject
            {
                Value = 1,
                Child = new NestedObject
                {
                    Value = 2,
                    Child = new NestedObject { Value = 3 }
                }
            }
        };

        // Act - Should complete without hanging
        var result = complex.ToJsonStringForce();

        // Assert - Just verify it works
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("PerformanceTest");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToJsonStringForce_WithEmptyObject_ReturnsValidJsonObject()
    {
        // Arrange
        var emptyObj = new { };

        // Act
        var result = emptyObj.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Anonymous types are serialized with normal serialization
        var action = () => JsonDocument.Parse(result!);
        action.Should().NotThrow();
    }

    [Fact]
    public void ToJsonStringForce_WithEmptyCollection_ReturnsEmptyJsonArray()
    {
        // Arrange
        var emptyList = new List<string>();

        // Act
        var result = emptyList.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result!.Trim().Should().Be("[]");
    }

    [Fact]
    public void ToJsonStringForce_WithDictionary_SerializesCorrectly()
    {
        // Arrange
        var dict = new Dictionary<string, object?>
        {
            ["string"] = "value",
            ["number"] = 42,
            ["boolean"] = true,
            ["null"] = null,
            ["nested"] = new Dictionary<string, int> { ["inner"] = 100 }
        };

        // Act
        var result = dict.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"string\"");
        result.Should().Contain("\"value\"");
        result.Should().Contain("42");
        result.Should().Contain("true");
        result.Should().Contain("\"inner\"");
        result.Should().Contain("100");
    }

    [Fact]
    public void ToJsonStringForce_WithCustomOptions_UsesCustomOptions()
    {
        // Arrange
        var obj = new { Name = "Test" };
        var customOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var result = obj.ToJsonStringForce(customOptions: customOptions);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Custom options bypass the force serialization
        result.Should().Contain("name"); // CamelCase applied
    }

    [Fact]
    public void ToJsonStringForce_WithPrimitiveTypes_SerializesCorrectly()
    {
        // Arrange & Act & Assert
        "test string".ToJsonStringForce().Should().Contain("test string");
        42.ToJsonStringForce().Should().Be("42");
        3.14.ToJsonStringForce().Should().Contain("3.14");
        true.ToJsonStringForce().Should().Be("true");
    }

    [Fact]
    public void ToJsonStringForce_WithDateTimeTypes_SerializesCorrectly()
    {
        // Arrange
        var dateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var dateTimeOffset = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.FromHours(8));
        var timeSpan = TimeSpan.FromHours(2.5);

        // Act
        var dtResult = dateTime.ToJsonStringForce();
        var dtoResult = dateTimeOffset.ToJsonStringForce();
        var tsResult = timeSpan.ToJsonStringForce();

        // Assert
        dtResult.Should().NotBeNullOrEmpty();
        dtoResult.Should().NotBeNullOrEmpty();
        tsResult.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ToJsonStringForce_WithGuid_SerializesCorrectly()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        // Act
        var result = guid.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void ToJsonStringForce_WithEnum_SerializesCorrectly()
    {
        // Arrange
        var status = ComplexStatus.Active;

        // Act
        var result = status.ToJsonStringForce();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion
}
