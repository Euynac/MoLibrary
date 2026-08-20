using Monica.Configuration.UI.Support;

namespace Test.Monica.Configuration.UI.Support;

public sealed class ConfigurationDebugValueRedactorTests
{
    [Theory]
    [InlineData("ConnectionStrings:Default")]
    [InlineData("Authentication:Jwt:Key")]
    [InlineData("Services:Billing:ClientSecret")]
    [InlineData("Storage:AccessToken")]
    [InlineData("Messaging__ApiKey")]
    public void IsSensitiveKey_ClassifiesCredentialLikePaths(string key)
    {
        Assert.True(ConfigurationDebugValueRedactor.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("Features:Enabled")]
    [InlineData("Runtime:TokenExpirationMinutes")]
    [InlineData("Logging:LogLevel:Default")]
    [InlineData("Service:Endpoint")]
    public void IsSensitiveKey_DoesNotHideOrdinarySettings(string key)
    {
        Assert.False(ConfigurationDebugValueRedactor.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("Server=localhost;Password=super-secret")]
    [InlineData("Endpoint=https://example.test;AccountKey=super-secret")]
    [InlineData("Bearer super-secret-token")]
    [InlineData("-----BEGIN PRIVATE KEY-----abc")]
    public void IsSensitiveValue_ClassifiesEmbeddedCredentialMaterial(string value)
    {
        Assert.True(ConfigurationDebugValueRedactor.IsSensitiveValue(value));
    }

    [Theory]
    [InlineData("Server=localhost;Database=operations")]
    [InlineData("token expiration is 30 minutes")]
    [InlineData("https://example.test/api")]
    public void IsSensitiveValue_DoesNotHideOrdinaryValues(string value)
    {
        Assert.False(ConfigurationDebugValueRedactor.IsSensitiveValue(value));
    }

    [Fact]
    public void Parse_MasksSensitiveValuesAndExcludesThemFromSearchText()
    {
        const string secret = "super-secret-value";
        var lines = ConfigurationDebugValueRedactor.Parse(
            $"Authentication:ClientSecret={secret}{Environment.NewLine}Features:Enabled=true");

        Assert.True(lines[0].IsSensitive);
        Assert.DoesNotContain(secret, lines[0].MaskedText);
        Assert.DoesNotContain(secret, lines[0].SearchText);
        Assert.Equal($"Authentication:ClientSecret={secret}", lines[0].GetDisplayText(revealed: true));
        Assert.False(lines[1].IsSensitive);
        Assert.Equal("Features:Enabled=true", lines[1].MaskedText);
    }

    [Fact]
    public void Parse_UsesHierarchicalDebugViewSectionsForSensitivity()
    {
        const string secret = "Server=localhost;Password=secret";
        var lines = ConfigurationDebugValueRedactor.Parse(
            $"ConnectionStrings:{Environment.NewLine}  Default={secret} (MemoryConfigurationProvider)");

        Assert.False(lines[0].IsSensitive);
        Assert.True(lines[1].IsSensitive);
        Assert.Equal("ConnectionStrings:Default", lines[1].Key);
        Assert.DoesNotContain(secret, lines[1].MaskedText);
    }

    [Fact]
    public void Parse_MasksCredentialMaterialEvenWhenTheKeyIsGeneric()
    {
        const string secret = "Server=localhost;Password=super-secret";
        var line = Assert.Single(ConfigurationDebugValueRedactor.Parse($"Database:Primary={secret}"));

        Assert.True(line.IsSensitive);
        Assert.DoesNotContain(secret, line.MaskedText);
        Assert.DoesNotContain(secret, line.SearchText);
    }
}
