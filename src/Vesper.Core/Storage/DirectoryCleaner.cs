namespace Vesper.Core.Storage;

public static class DirectoryCleaner
{
    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
            return;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                ClearReadOnly(path);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            Thread.Sleep(200);
        }

        DeleteContentsBestEffort(path);

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
        }
    }

    private static void ClearReadOnly(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                var attributes = File.GetAttributes(file);

                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void DeleteContentsBestEffort(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception)
            {
            }
        }
    }
}
