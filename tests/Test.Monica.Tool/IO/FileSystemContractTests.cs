using Monica.Tool.IO;

namespace Test.Monica.Tool.IO;

public sealed class FileSystemContractTests
{
    [Fact]
    public void IsDirectory_WhenPathDoesNotExist_ShouldReturnFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monica-tool-missing-{Guid.NewGuid():N}");

        FileSystem.IsDirectory(path).Should().BeFalse();
    }

    [Fact]
    public void ReadEmbeddedResource_WhenResourceDoesNotExist_ShouldReturnNull()
    {
        FileSystem.ReadEmbeddedResource($"missing-{Guid.NewGuid():N}.txt").Should().BeNull();
    }

    [Theory]
    [InlineData("IO.Resources.Embedded.txt")]
    [InlineData("Test.Monica.Tool.IO.Resources.Embedded.txt")]
    public void ReadEmbeddedResource_WhenResourceExists_ShouldResolveShortAndManifestNames(string resourceName)
    {
        FileSystem.ReadEmbeddedResource(resourceName).Should().Be("embedded resource content\n");
    }

    [Fact]
    public void WriteAppendDelete_WhenNestedPathIsProvided_ShouldCompleteRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), $"monica-tool-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "nested", "value.txt");

        try
        {
            FileSystem.WriteFile(path, "first");
            FileSystem.AppendFile(path, " second");

            File.ReadAllText(path).Should().Be("first second");
            FileSystem.Delete(path).Should().BeTrue();
            FileSystem.Delete(path).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Rename_WhenSourceExists_ShouldMoveFileAndReturnNewInfo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"monica-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "before.txt");
        File.WriteAllText(sourcePath, "value");

        try
        {
            var renamed = new FileInfo(sourcePath).Rename("after.txt");

            renamed.Should().NotBeNull();
            renamed!.Name.Should().Be("after.txt");
            renamed.Exists.Should().BeTrue();
            File.Exists(sourcePath).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
