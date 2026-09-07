using AwesomeAssertions;
using Monica.Repository.Persistence.Models;
using Monica.Repository.Persistence.Services.Support;
using Xunit;

namespace Test.Monica.Repository.Persistence.Services.Support;

public sealed class RepositoryConnectionPoolInfoParserTests
{
    [Fact]
    public void Parse_WhenPoolKeywordsPresent_ShouldReturnExplicitValues()
    {
        var pool = RepositoryConnectionPoolInfoParser.Parse(
            "Host=localhost;Database=app;Username=app;Max Pool Size=50;Min Pool Size=5;Pooling=true");

        pool.Should().NotBeNull();
        pool!.Pooling.Should().BeTrue();
        pool.MaxPoolSize.Should().Be(50);
        pool.MinPoolSize.Should().Be(5);
        pool.PoolingEnabled.Should().BeTrue();
        pool.EffectiveMaxPoolSize.Should().Be(50);
        pool.EffectiveMinPoolSize.Should().Be(5);
        pool.MaxPoolSizeIsDefault.Should().BeFalse();
        pool.MinPoolSizeIsDefault.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenSynonymKeywordsUsed_ShouldReturnExplicitValues()
    {
        var pool = RepositoryConnectionPoolInfoParser.Parse(
            "Data Source=localhost;Maximum Pool Size=30;Minimum Pool Size=2");

        pool.Should().NotBeNull();
        pool!.MaxPoolSize.Should().Be(30);
        pool.MinPoolSize.Should().Be(2);
    }

    [Fact]
    public void Parse_WhenKeywordsAreCaseInsensitive_ShouldReturnExplicitValues()
    {
        var pool = RepositoryConnectionPoolInfoParser.Parse("max pool size=25;min pool size=1;pooling=false");

        pool.Should().NotBeNull();
        pool!.MaxPoolSize.Should().Be(25);
        pool.MinPoolSize.Should().Be(1);
        pool.Pooling.Should().BeFalse();
        pool.PoolingEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenKeywordsOmitted_ShouldFallBackToSharedDefaults()
    {
        var pool = RepositoryConnectionPoolInfoParser.Parse("Host=localhost;Database=app");

        pool.Should().NotBeNull();
        pool!.Pooling.Should().BeNull();
        pool.MaxPoolSize.Should().BeNull();
        pool.MinPoolSize.Should().BeNull();
        pool.PoolingEnabled.Should().BeTrue();
        pool.EffectiveMaxPoolSize.Should().Be(RepositoryConnectionPoolInfo.DefaultMaxPoolSize);
        pool.EffectiveMinPoolSize.Should().Be(RepositoryConnectionPoolInfo.DefaultMinPoolSize);
        pool.MaxPoolSizeIsDefault.Should().BeTrue();
        pool.MinPoolSizeIsDefault.Should().BeTrue();
    }

    [Theory]
    [InlineData("Max Pool Size=not-a-number")]
    [InlineData("Max Pool Size=-5")]
    [InlineData("Max Pool Size=")]
    public void Parse_WhenMaxPoolSizeInvalid_ShouldIgnoreValue(string connectionString)
    {
        var pool = RepositoryConnectionPoolInfoParser.Parse(connectionString);

        pool.Should().NotBeNull();
        pool!.MaxPoolSize.Should().BeNull();
        pool.MaxPoolSizeIsDefault.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WhenConnectionStringEmpty_ShouldReturnNull(string? connectionString)
    {
        RepositoryConnectionPoolInfoParser.Parse(connectionString).Should().BeNull();
    }

    [Fact]
    public void Parse_WhenConnectionStringMalformed_ShouldReturnNull()
    {
        RepositoryConnectionPoolInfoParser.Parse("not a valid connection string").Should().BeNull();
    }
}
