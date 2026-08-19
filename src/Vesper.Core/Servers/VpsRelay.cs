using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed class VpsRelayConfig
{
    public string Host { get; set; } = string.Empty;

    public string User { get; set; } = "root";

    public int SshPort { get; set; } = 22;

    public string KeyPath { get; set; } = string.Empty;

    public int RemotePort { get; set; } = 6969;

    public string WorkDir { get; set; } = "vesper-relay";

    public static string FilePath(VesperPaths paths) =>
        Path.Combine(paths.RuntimeDir, "vps", "relay.json");

    public static VpsRelayConfig Load(VesperPaths paths)
    {
        var path = FilePath(paths);

        if (!File.Exists(path))
            return new VpsRelayConfig();

        try
        {
            return JsonSerializer.Deserialize<VpsRelayConfig>(File.ReadAllText(path))
                ?? new VpsRelayConfig();
        }
        catch (Exception)
        {
            return new VpsRelayConfig();
        }
    }

    public void Save(VesperPaths paths)
    {
        var path = FilePath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class VpsRelay : IDisposable
{
    public const int InnerPortOffset = 1;

    private readonly VpsRelayConfig _config;
    private readonly int _localPort;
    private Process? _tunnel;

    public VpsRelay(VpsRelayConfig config, int localPort)
    {
        _config = config;
        _localPort = localPort;
    }

    public event EventHandler<string>? Output;

    public bool IsRunning => _tunnel is { HasExited: false };

    public string JoinAddress => $"{_config.Host}:{_config.RemotePort}";

    public async Task<string> DetectOsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            "cat /etc/os-release 2>/dev/null | grep -m1 PRETTY_NAME || uname -s || echo unknown",
            cancellationToken);

        var text = result.Trim();
        Output?.Invoke(this, "OS: " + text);
        return text;
    }

    public async Task<int> ChooseRemotePortAsync(CancellationToken cancellationToken = default)
    {
        var candidate = _config.RemotePort;

        for (var i = 0; i < 12; i++)
        {
            var probe = await RunAsync(
                $"(ss -ltn 2>/dev/null || netstat -ltn 2>/dev/null) | grep -q ':{candidate} ' && echo taken || echo free",
                cancellationToken);

            if (probe.Contains("free"))
            {
                _config.RemotePort = candidate;
                Output?.Invoke(this, "Using remote port " + candidate);
                return candidate;
            }

            Output?.Invoke(this, "Port " + candidate + " is in use, trying the next one");
            candidate++;
        }

        return _config.RemotePort;
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        var inner = _config.RemotePort + InnerPortOffset;

        var script = string.Join(" ; ", new[]
        {
            $"mkdir -p ~/{_config.WorkDir}",
            "command -v socat >/dev/null 2>&1 || " +
            "(sudo -n apt-get update -y && sudo -n apt-get install -y socat) 2>/dev/null || " +
            "sudo -n dnf install -y socat 2>/dev/null || " +
            "sudo -n yum install -y socat 2>/dev/null || " +
            "sudo -n apk add socat 2>/dev/null || echo VESPER_NEED_SOCAT",
            $"sudo -n ufw allow {_config.RemotePort}/tcp 2>/dev/null || true",
            $"sudo -n firewall-cmd --add-port={_config.RemotePort}/tcp 2>/dev/null || true",
        });

        var output = await RunAsync(script, cancellationToken);

        if (output.Contains("VESPER_NEED_SOCAT"))
        {
            throw new InvalidOperationException(
                "socat is not installed on the VPS and could not be installed automatically. " +
                "Install it manually (for example: sudo apt-get install -y socat) and try again.");
        }

        Output?.Invoke(this, $"Prepared. Forwarding {_config.Host}:{_config.RemotePort} -> your PC:{_localPort} (inner {inner})");
    }

    public Task<string> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.FromResult(JoinAddress);

        var inner = _config.RemotePort + InnerPortOffset;

        var remote = string.Join(" ; ", new[]
        {
            $"fuser -k {_config.RemotePort}/tcp 2>/dev/null || true",
            $"echo VESPER_RELAY_READY {_config.Host}:{_config.RemotePort}",
            $"socat TCP-LISTEN:{_config.RemotePort},fork,reuseaddr TCP:127.0.0.1:{inner}",
        });

        var info = new ProcessStartInfo
        {
            FileName = "ssh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        AddCommonSshArgs(info);
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add("ExitOnForwardFailure=yes");
        info.ArgumentList.Add("-R");
        info.ArgumentList.Add($"127.0.0.1:{inner}:localhost:{_localPort}");
        info.ArgumentList.Add($"{_config.User}@{_config.Host}");
        info.ArgumentList.Add(remote);

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Output?.Invoke(this, e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Output?.Invoke(this, e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _tunnel = process;

        return Task.FromResult(JoinAddress);
    }

    public void Stop()
    {
        if (_tunnel is null)
            return;

        try
        {
            if (!_tunnel.HasExited)
                _tunnel.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        _tunnel.Dispose();
        _tunnel = null;
    }

    public void Dispose() => Stop();

    private async Task<string> RunAsync(string remoteCommand, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = "ssh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        AddCommonSshArgs(info);
        info.ArgumentList.Add($"{_config.User}@{_config.Host}");
        info.ArgumentList.Add(remoteCommand);

        using var process = new Process { StartInfo = info };
        var buffer = new StringBuilder();

        process.OutputDataReceived += (_, e) => Append(buffer, e.Data);
        process.ErrorDataReceived += (_, e) => Append(buffer, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return buffer.ToString();
    }

    private void AddCommonSshArgs(ProcessStartInfo info)
    {
        info.ArgumentList.Add("-i");
        info.ArgumentList.Add(_config.KeyPath);
        info.ArgumentList.Add("-p");
        info.ArgumentList.Add(_config.SshPort.ToString());
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add("StrictHostKeyChecking=accept-new");
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add("BatchMode=yes");
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add("ServerAliveInterval=30");
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add("ConnectTimeout=15");
    }

    private void Append(StringBuilder buffer, string? line)
    {
        if (line is null)
            return;

        buffer.AppendLine(line);
        Output?.Invoke(this, line);
    }
}
