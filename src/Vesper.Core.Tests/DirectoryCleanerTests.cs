using Vesper.Core.Storage;
using Xunit;

namespace Vesper.Core.Tests;

public class DirectoryCleanerTests
{
    [Fact]
    public void Delete_removes_directory_with_read_only_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "vesper-clean-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "mods", "natives");
        Directory.CreateDirectory(nested);

        var locked = Path.Combine(nested, "readonly.dll");
        File.WriteAllText(locked, "x");
        File.SetAttributes(locked, FileAttributes.ReadOnly);

        DirectoryCleaner.Delete(root);

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Delete_is_a_no_op_when_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "vesper-missing-" + Guid.NewGuid().ToString("N"));

        DirectoryCleaner.Delete(missing);

        Assert.False(Directory.Exists(missing));
    }
}
