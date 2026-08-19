using System.Net;
using System.Text.Json;
using Renci.SshNet;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed class VpsRelayConfig
{
    public string Host { get; set; } = string.Empty;

    public string User { get; set; } = "root";

    public int SshPort { get; set; } = 22;

    public string AuthMode { get; set; } = "key";

    public string KeyPath { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

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

    private const string SudoPrefix =
        "SUDO=; if [ \"$(id -u)\" -ne 0 ]; then SUDO='sudo -n'; fi ; ";

    private readonly VpsRelayConfig _config;
    private readonly int _localPort;

    private SshClient? _client;
    private ForwardedPortRemote? _forward;
    private SshCommand? _socat;

    public VpsRelay(VpsRelayConfig config, int localPort)
    {
        _config = config;
        _localPort = localPort;
    }

    public event EventHandler<string>? Output;

    public bool IsRunning => _forward is { IsStarted: true };

    public string JoinAddress => $"{_config.Host}:{_config.RemotePort}";

    private bool UsesPassword =>
        string.Equals(_config.AuthMode, "password", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrEmpty(_config.Password);

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

        var script = SudoPrefix + string.Join(" ; ", new[]
        {
            $"mkdir -p ~/{_config.WorkDir}",
            "command -v socat >/dev/null 2>&1 || " +
            "( $SUDO apt-get update -y && $SUDO apt-get install -y socat ) 2>/dev/null || " +
            "$SUDO dnf install -y socat 2>/dev/null || " +
            "$SUDO yum install -y socat 2>/dev/null || " +
            "$SUDO apk add socat 2>/dev/null || true",
            "command -v socat >/dev/null 2>&1 || echo VESPER_NEED_SOCAT",
            $"$SUDO ufw allow {_config.RemotePort}/tcp 2>/dev/null || true",
            $"$SUDO firewall-cmd --add-port={_config.RemotePort}/tcp 2>/dev/null || true",
            "$SUDO firewall-cmd --reload 2>/dev/null || true",
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

        return Task.Run(() =>
        {
            var client = Connect();
            var inner = _config.RemotePort + InnerPortOffset;

            client.RunCommand(SudoPrefix + $"$SUDO fuser -k {_config.RemotePort}/tcp 2>/dev/null || true");

            _forward = new ForwardedPortRemote(
                IPAddress.Loopback, (uint)inner, IPAddress.Loopback, (uint)_localPort);

            _forward.Exception += (_, e) => Output?.Invoke(this, "forward error: " + e.Exception.Message);

            client.AddForwardedPort(_forward);
            _forward.Start();

            Output?.Invoke(this, $"VESPER_RELAY_READY {_config.Host}:{_config.RemotePort}");

            _socat = client.CreateCommand(
                $"socat TCP-LISTEN:{_config.RemotePort},fork,reuseaddr TCP:127.0.0.1:{inner}");
            _socat.BeginExecute();

            return JoinAddress;
        }, cancellationToken);
    }

    public void Stop()
    {
        try
        {
            _socat?.CancelAsync();
        }
        catch (Exception)
        {
        }

        _socat?.Dispose();
        _socat = null;

        try
        {
            if (_forward is { IsStarted: true })
                _forward.Stop();
        }
        catch (Exception)
        {
        }

        _forward?.Dispose();
        _forward = null;

        try
        {
            if (_client is { IsConnected: true })
                _client.Disconnect();
        }
        catch (Exception)
        {
        }

        _client?.Dispose();
        _client = null;
    }

    public void Dispose() => Stop();

    private Task<string> RunAsync(string command, CancellationToken cancellationToken) =>
        Task.Run(() => Run(command), cancellationToken);

    private string Run(string command)
    {
        var client = Connect();

        using var cmd = client.CreateCommand(command);
        var output = cmd.Execute() ?? string.Empty;
        var combined = output + (cmd.Error ?? string.Empty);

        foreach (var raw in combined.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.Length > 0)
                Output?.Invoke(this, line);
        }

        return combined;
    }

    private SshClient Connect()
    {
        if (_client is { IsConnected: true })
            return _client;

        _client?.Dispose();

        var client = new SshClient(BuildConnectionInfo());
        client.KeepAliveInterval = TimeSpan.FromSeconds(30);
        client.Connect();
        _client = client;
        return client;
    }

    private ConnectionInfo BuildConnectionInfo()
    {
        AuthenticationMethod auth;

        if (UsesPassword)
        {
            auth = new PasswordAuthenticationMethod(_config.User, _config.Password);
        }
        else
        {
            if (!File.Exists(_config.KeyPath))
                throw new InvalidOperationException("SSH key file not found: " + _config.KeyPath);

            auth = new PrivateKeyAuthenticationMethod(_config.User, new PrivateKeyFile(_config.KeyPath));
        }

        return new ConnectionInfo(_config.Host, _config.SshPort, _config.User, auth)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }
}
