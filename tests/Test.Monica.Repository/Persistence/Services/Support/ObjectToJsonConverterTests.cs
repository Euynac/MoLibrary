using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Services;
using Monica.Repository.Persistence.Services.Support;
using Monica.UnitTests.Repository;
using Xunit;

namespace Test.Monica.Repository.Persistence.Services.Support;

public sealed class ObjectToJsonConverterTests
{
    [Fact]
    public async Task HasJsonConversion_WhenNestedValueIsMutated_ShouldPersistChange()
    {
        await using var fixture = await DbContextFixture<JsonConversionDbContext>
            .UseSqliteInMemory()
            .EnsureCreatedAsync(TestContext.Current.CancellationToken);
        fixture.Context.Entries.Add(new JsonEntry
        {
            Id = 1,
            Content = new JsonContent
            {
                Items = [new JsonItem { Name = "before" }]
            }
        });
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Context.ChangeTracker.Clear();

        var tracked = await fixture.Context.Entries.SingleAsync(
            entry => entry.Id == 1,
            TestContext.Current.CancellationToken);
        tracked.Content.Items[0].Name = "after";

        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Context.ChangeTracker.Clear();

        var reloaded = await fixture.Context.Entries.SingleAsync(
            entry => entry.Id == 1,
            TestContext.Current.CancellationToken);
        reloaded.Content.Items[0].Name.Should().Be("after");
    }

    [Fact]
    public async Task HasJsonConversion_WhenCustomOptionsProvided_ShouldUseThemForStoredJson()
    {
        await using var fixture = await DbContextFixture<JsonConversionDbContext>
            .UseSqliteInMemory()
            .EnsureCreatedAsync(TestContext.Current.CancellationToken);
        fixture.Context.OptionEntries.Add(new JsonOptionsEntry
        {
            Id = 2,
            Content = new JsonOptionsContent
            {
                HeaderRowIndex = 0,
                Description = null
            }
        });
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var command = fixture.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Content FROM JsonOptionsEntries WHERE Id = 2";
        var json = (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        json.Should().Contain("\"HeaderRowIndex\":0");
        json.Should().NotContain("Description");
    }
}

public sealed class JsonConversionDbContext(
    DbContextOptions<JsonConversionDbContext> options,
    ICachedServiceProvider serviceProvider)
    : RepositoryDbContext<JsonConversionDbContext>(options, serviceProvider)
{
    public DbSet<JsonEntry> Entries => Set<JsonEntry>();
    public DbSet<JsonOptionsEntry> OptionEntries => Set<JsonOptionsEntry>();
}

public sealed class JsonEntry : Entity<long>
{
    public JsonContent Content { get; set; } = new();
}

public sealed class JsonContent
{
    public List<JsonItem> Items { get; set; } = [];
}

public sealed class JsonItem
{
    public string Name { get; set; } = "";
}

public sealed class JsonOptionsEntry : Entity<long>
{
    public JsonOptionsContent Content { get; set; } = new();
}

public sealed class JsonOptionsContent
{
    public int HeaderRowIndex { get; set; }
    public string? Description { get; set; }
}

public sealed class JsonEntryConfiguration : IEntityTypeConfiguration<JsonEntry>
{
    public void Configure(EntityTypeBuilder<JsonEntry> builder)
    {
        builder.ToTable("JsonEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Content).HasJsonConversion();
    }
}

public sealed class JsonOptionsEntryConfiguration : IEntityTypeConfiguration<JsonOptionsEntry>
{
    public void Configure(EntityTypeBuilder<JsonOptionsEntry> builder)
    {
        builder.ToTable("JsonOptionsEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Content).HasJsonConversion(
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
    }
}
