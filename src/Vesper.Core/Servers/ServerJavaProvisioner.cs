using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed class ServerJavaProvisioner
{
    private readonly VesperPaths _paths;
    private readonly HttpClient _http;

    public ServerJavaProvisioner(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? new HttpClient();
    }

    public static int RequiredMajor(string minecraftVersion)
    {
        var (minor, patch) = ParseVersion(minecraftVersion);

        if (minor >= 21)
            return 21;

        if (minor == 20 && patch >= 5)
            return 21;

        if (minor >= 17)
            return 17;

        return 8;
    }

    public string JavaHomeDir(int major) =>
        Path.Combine(_paths.RuntimeDir, "serverjava", major.ToString());

    public string? ExistingJava(int major)
    {
        var dir = JavaHomeDir(major);

        if (!Directory.Exists(dir))
            return null;

        var name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

        return Directory
            .EnumerateFiles(dir, name, SearchOption.AllDirectories)
            .FirstOrDefault(p => Path.GetFileName(Path.GetDirectoryName(p)) == "bin");
    }

    public static string DownloadUrl(int major)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac"
            : "linux";

        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x64";

        return $"https://api.adoptium.net/v3/binary/latest/{major}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";
    }

    public async Task<string> EnsureAsync(
        string minecraftVersion,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var major = RequiredMajor(minecraftVersion);
        var existing = ExistingJava(major);

        if (existing is not null)
            return existing;

        var dir = JavaHomeDir(major);
        Directory.CreateDirectory(dir);

        progress?.Report($"Downloading Java {major} to run Minecraft {minecraftVersion}");

        var url = DownloadUrl(major);
        var bytes = await _http.GetByteArrayAsync(url, cancellationToken);
        var isZip = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var archive = Path.Combine(dir, isZip ? "java.zip" : "java.tar.gz");
        await File.WriteAllBytesAsync(archive, bytes, cancellationToken);

        progress?.Report($"Extracting Java {major}");

        if (isZip)
        {
            ZipFile.ExtractToDirectory(archive, dir, true);
        }
        else
        {
            await using var input = File.OpenRead(archive);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, dir, true, cancellationToken);
        }

        File.Delete(archive);

        var java = ExistingJava(major)
            ?? throw new InvalidOperationException(
                $"Downloaded Java {major} but could not find its java executable.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            MakeExecutable(java);

        return java;
    }

    private static (int Minor, int Patch) ParseVersion(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = parts.Length > 1 && int.TryParse(Digits(parts[1]), out var m) ? m : 0;
        var patch = parts.Length > 2 && int.TryParse(Digits(parts[2]), out var p) ? p : 0;
        return (minor, patch);
    }

    private static string Digits(string value)
    {
        var end = 0;
        while (end < value.Length && char.IsDigit(value[end]))
            end++;

        return value[..end];
    }

    private static void MakeExecutable(string path)
    {
        try
        {
            using var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = "+x \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            chmod?.WaitForExit(5000);
        }
        catch (Exception)
        {
        }
    }
}
