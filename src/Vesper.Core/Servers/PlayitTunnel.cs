using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed partial class PlayitTunnel : IDisposable
{
    public const string ReleaseBase =
        "https://github.com/playit-cloud/playit-agent/releases/latest/download/";

    private readonly VesperPaths _paths;
    private readonly HttpClient _http;
    private Process? _process;

    public PlayitTunnel(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? new HttpClient();
    }

    public event EventHandler<string>? ClaimUrlFound;

    public event EventHandler<string>? TunnelAddressFound;

    public event EventHandler<string>? Output;

    public bool IsRunning => _process is { HasExited: false };

    public string BinaryDirectory => Path.Combine(_paths.RuntimeDir, "playit");

    public string BinaryPath => Path.Combine(BinaryDirectory, AssetName());

    public static string AssetName()
    {
        var arch = RuntimeInformation.OSArchitecture;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return arch == Architecture.Arm64 ? "playit-windows-aarch64.exe" : "playit-windows-x86_64.exe";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return arch == Architecture.Arm64 ? "playit-darwin-aarch64" : "playit-darwin-intel";

        return arch == Architecture.Arm64 ? "playit-linux-aarch64" : "playit-linux-amd64";
    }

    public async Task EnsureBinaryAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(BinaryPath))
            return;

        Directory.CreateDirectory(BinaryDirectory);
        progress?.Report("Downloading the playit agent");

        var bytes = await _http.GetByteArrayAsync(ReleaseBase + AssetName(), cancellationToken);
        await File.WriteAllBytesAsync(BinaryPath, bytes, cancellationToken);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            MakeExecutable(BinaryPath);
    }

    public async Task StartAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        await EnsureBinaryAsync(progress, cancellationToken);

        var info = new ProcessStartInfo
        {
            FileName = BinaryPath,
            WorkingDirectory = BinaryDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _process = process;
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

    private void HandleLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        Output?.Invoke(this, line);

        var claim = ClaimRegex().Match(line);
        if (claim.Success)
            ClaimUrlFound?.Invoke(this, claim.Value);

        var address = ParseAddress(line);
        if (address is not null)
            TunnelAddressFound?.Invoke(this, address);
    }

    public static string? ParseAddress(string line)
    {
        var host = TunnelHostRegex().Match(line);
        if (host.Success)
            return host.Value;

        var ipPort = IpPortRegex().Match(line);
        return ipPort.Success ? ipPort.Value : null;
    }

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

    [GeneratedRegex(@"https://playit\.gg/[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex ClaimRegex();

    [GeneratedRegex(@"[a-z0-9-]+\.(?:craft\.)?(?:joinmc\.link|playit\.gg)(?::\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex TunnelHostRegex();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}:\d{2,5}\b")]
    private static partial Regex IpPortRegex();
}
