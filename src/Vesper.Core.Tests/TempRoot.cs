using Vesper.Core.Storage;

namespace Vesper.Core.Tests;

public sealed class TempRoot : IDisposable
{
    public TempRoot()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vesper-tests", Guid.NewGuid().ToString("N"));
        Paths = new VesperPaths(Directory);
        Paths.EnsureCreated();
    }

    public string Directory { get; }

    public VesperPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
