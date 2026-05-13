using AwesomeAssertions;
using Monica.Configuration.Models;
using Xunit;

namespace Test.Monica.Configuration.Paths;

public class LogicalPathTests
{
    [Fact]
    public void Append_WhenSegmentIsAdded_ShouldReturnNewPathWithoutMutatingOriginal()
    {
        var original = LogicalPath.FromProperties("Services");

        var updated = original.Append(new PropertySegment("Billing"));

        original.Segments.Should().ContainSingle()
            .Which.Should().Be(new PropertySegment("Services"));
        updated.Segments.Should().Equal(new PropertySegment("Services"), new PropertySegment("Billing"));
    }

    [Fact]
    public void FromProperties_WhenPropertiesAreProvided_ShouldProducePropertySegments()
    {
        var path = LogicalPath.FromProperties("RagOptions", "WorkerId");

        path.Segments.Should().Equal(new PropertySegment("RagOptions"), new PropertySegment("WorkerId"));
        path.Depth.Should().Be(2);
    }

    [Fact]
    public void Parse_WhenCanonicalContainsAllSegmentKinds_ShouldRoundTripOriginalPath()
    {
        var original = new LogicalPath(
        [
            new PropertySegment("Services.Root"),
            new DictionaryKeySegment("billing[main].db"),
            new PropertySegment("ConnectedDbs"),
            new ListItemKeySegment("primary.01"),
            new ListIndexSegment(7),
            new PropertySegment("ConnectionString")
        ]);

        var parsed = LogicalPath.Parse(original.ToCanonicalString());

        parsed.Should().Be(original);
    }
}
