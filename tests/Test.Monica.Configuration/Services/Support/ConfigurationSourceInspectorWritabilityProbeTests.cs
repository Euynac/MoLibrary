using AwesomeAssertions;
using Monica.Configuration.Services.Support;
using Xunit;

namespace Test.Monica.Configuration.Services.Support;

public sealed class ConfigurationSourceInspectorWritabilityProbeTests
{
    [Fact]
    public void CanWriteJsonFile_WhenFileIsReadOnly_ShouldReturnFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monica-readonly-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{}");
        try
        {
            File.SetAttributes(path, FileAttributes.ReadOnly);

            ConfigurationSourceInspector.CanWriteJsonFile(path).Should().BeFalse();
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    [Fact]
    public void CanWriteJsonFile_WhenFileIsWritable_ShouldReturnTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"monica-writable-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{}");
        try
        {
            ConfigurationSourceInspector.CanWriteJsonFile(path).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CanWriteJsonFile_WhenFileIsMissingButDirectoryIsWritable_ShouldReturnTrueWithoutLeavingFiles()
    {
        var directory = Directory.CreateTempSubdirectory("monica-probe-");
        try
        {
            var path = Path.Combine(directory.FullName, "absent.json");

            ConfigurationSourceInspector.CanWriteJsonFile(path).Should().BeTrue();
            File.Exists(path).Should().BeFalse();
            Directory.GetFiles(directory.FullName).Should().BeEmpty();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void CanWriteJsonFile_WhenDirectoryDoesNotExist_ShouldReturnFalse()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"monica-missing-dir-{Guid.NewGuid():N}",
            "file.json");

        ConfigurationSourceInspector.CanWriteJsonFile(path).Should().BeFalse();
    }
}
