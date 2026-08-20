using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Monica.Dapr.Services.Support;
using Monica.StateStore.Queries;
using Xunit;

namespace Test.Monica.Dapr.Services;

public sealed class DaprStateQueryRendererTests
{
    [Fact]
    public void Render_ShouldUseCanonicalJsonMetadataAndOmitUnselectedPagingFields()
    {
        var builder = new QueryBuilder<QueryDocument>();
        var query = builder
            .WithPaging("next-page")
            .Where(filter => filter.And(
                new FilterQuery<QueryDocument>().Eq(document => document.Owner.DisplayName, "Monica"),
                new FilterQuery<QueryDocument>().GTE(
                    document => document.CreatedAt,
                    new DateTime(2026, 8, 12, 9, 30, 0, DateTimeKind.Utc))))
            .Sort(document => document.Owner.DisplayName, Ordering.Descending)
            .Build();

        Assert.Same(builder, query);
        using var document = JsonDocument.Parse(
            DaprStateQueryRenderer.Render(builder.BuildDefinition(), CreateSerializerOptions()));
        var root = document.RootElement;

        Assert.Equal(
            "Monica",
            root.GetProperty("filter").GetProperty("AND")[0]
                .GetProperty("EQ").GetProperty("owner.display_name").GetString());
        Assert.Equal(
            new DateTime(2026, 8, 12, 9, 30, 0, DateTimeKind.Utc),
            root.GetProperty("filter").GetProperty("AND")[1]
                .GetProperty("GTE").GetProperty("createdAt").GetDateTime());
        Assert.Equal("owner.display_name", root.GetProperty("sort")[0].GetProperty("key").GetString());
        Assert.Equal("DESC", root.GetProperty("sort")[0].GetProperty("order").GetString());
        Assert.Equal("next-page", root.GetProperty("page").GetProperty("token").GetString());
        Assert.False(root.GetProperty("page").TryGetProperty("limit", out _));
    }

    [Fact]
    public void Render_WhenSelectedPropertyIsIgnored_ShouldRejectTheCanonicalContractMismatch()
    {
        var builder = new QueryBuilder<QueryDocument>();
        builder.Where(filter => filter.Eq(document => document.Ignored, "secret")).Build();

        Action render = () => DaprStateQueryRenderer.Render(
            builder.BuildDefinition(),
            CreateSerializerOptions());

        var exception = Assert.Throws<InvalidOperationException>(render);
        Assert.Contains("Ignored", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ignored or unavailable", exception.Message, StringComparison.Ordinal);
        Assert.Contains("host JSON contract", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenReferencePreservationIsEnabled_ShouldKeepInValuesAsAProtocolArray()
    {
        var builder = new QueryBuilder<QueryDocument>();
        builder.Where(filter => filter.In(
            document => document.Owner.DisplayName,
            "Monica",
            "Codex")).Build();

        using var document = JsonDocument.Parse(DaprStateQueryRenderer.Render(
            builder.BuildDefinition(),
            CreateSerializerOptions(preserveReferences: true)));
        var values = document.RootElement.GetProperty("filter").GetProperty("IN")
            .GetProperty("owner.display_name");

        Assert.Equal(JsonValueKind.Array, values.ValueKind);
        Assert.Equal(
            ["Monica", "Codex"],
            values.EnumerateArray().Select(static item => item.GetString()!).ToArray());
    }

    private static JsonSerializerOptions CreateSerializerOptions(bool preserveReferences = false)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = preserveReferences ? ReferenceHandler.Preserve : null,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        options.MakeReadOnly();
        return options;
    }

    private sealed class QueryDocument
    {
        public required QueryOwner Owner { get; init; }

        public DateTime CreatedAt { get; init; }

        [JsonIgnore]
        public string Ignored { get; init; } = string.Empty;
    }

    private sealed class QueryOwner
    {
        [JsonPropertyName("display_name")]
        public required string DisplayName { get; init; }
    }
}
