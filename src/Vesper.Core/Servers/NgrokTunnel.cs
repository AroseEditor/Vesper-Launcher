using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed class NgrokTunnel : IDisposable
{
    public const string LocalApi = "http://127.0.0.1:4040/api/tunnels";

    public const string DefaultAuthToken = "";

    private readonly VesperPaths _paths;
    private readonly HttpClient _http;
    private Process? _process;

    public NgrokTunnel(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? new HttpClient();
    }

    public event EventHandler<string>? Output;

    public bool IsRunning => _process is { HasExited: false };

    public string BinaryDirectory => Path.Combine(_paths.RuntimeDir, "ngrok");

    public string BinaryPath => Path.Combine(
        BinaryDirectory,
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ngrok.exe" : "ngrok");

    public string TokenFile => Path.Combine(BinaryDirectory, "authtoken.txt");

    public static string DownloadUrl()
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
        const string root = "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{root}windows-{arch}.zip";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"{root}darwin-{arch}.zip";

        return $"{root}linux-{arch}.tgz";
    }

    public string? SavedToken() => File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : null;

    public void SaveToken(string token)
    {
        Directory.CreateDirectory(BinaryDirectory);
        File.WriteAllText(TokenFile, token.Trim());
    }

    public async Task EnsureBinaryAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(BinaryPath))
            return;

        Directory.CreateDirectory(BinaryDirectory);
        progress?.Report("Downloading ngrok");

        var url = DownloadUrl();
        var bytes = await _http.GetByteArrayAsync(url, cancellationToken);
        var archive = Path.Combine(BinaryDirectory, url.EndsWith(".tgz") ? "ngrok.tgz" : "ngrok.zip");
        await File.WriteAllBytesAsync(archive, bytes, cancellationToken);

        progress?.Report("Extracting ngrok");

        if (url.EndsWith(".tgz"))
        {
            await using var input = File.OpenRead(archive);
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, BinaryDirectory, true, cancellationToken);
        }
        else
        {
            ZipFile.ExtractToDirectory(archive, BinaryDirectory, true);
        }

        File.Delete(archive);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(BinaryPath))
            MakeExecutable(BinaryPath);
    }

    public async Task<string?> StartAsync(
        int port,
        string authToken,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authToken))
            throw new InvalidOperationException(
                "An ngrok authtoken is required. Get a free one at dashboard.ngrok.com and paste it in.");

        if (IsRunning)
            Stop();

        await EnsureBinaryAsync(progress, cancellationToken);
        SaveToken(authToken);

        var info = new ProcessStartInfo
        {
            FileName = BinaryPath,
            WorkingDirectory = BinaryDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add("tcp");
        info.ArgumentList.Add(port.ToString());
        info.ArgumentList.Add("--authtoken");
        info.ArgumentList.Add(authToken.Trim());
        info.ArgumentList.Add("--log");
        info.ArgumentList.Add("stdout");

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Output?.Invoke(this, e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Output?.Invoke(this, e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        progress?.Report("Connecting to ngrok");
        return await WaitForAddressAsync(cancellationToken);
    }

    private async Task<string?> WaitForAddressAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process is { HasExited: true })
                return null;

            try
            {
                var json = await _http.GetStringAsync(LocalApi, cancellationToken);
                var address = ParsePublicAddress(json);

                if (address is not null)
                    return address;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    public static string? ParsePublicAddress(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("tunnels", out var tunnels))
            return null;

        foreach (var tunnel in tunnels.EnumerateArray())
        {
            if (tunnel.TryGetProperty("public_url", out var url))
            {
                var value = url.GetString();

                if (!string.IsNullOrEmpty(value))
                    return value.Replace("tcp://", string.Empty);
            }
        }

        return null;
    }

    public void Stop()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
        _process = null;
    }

    public void Dispose() => Stop();

    private static void MakeExecutable(string path)
    {
        try
        {
            using var chmod = Process.Start(new ProcessStartInfo
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
