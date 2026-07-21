using System.Diagnostics;

namespace Vesper.Core.Servers;

public sealed class ServerProcess : IDisposable
{
    private readonly object _gate = new();
    private Process? _process;

    public event EventHandler<string>? Output;

    public event EventHandler<bool>? RunningChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _process is { HasExited: false };
        }
    }

    public void Start(string javaPath, string workingDirectory, string jarFileName, int memoryMb)
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
                throw new InvalidOperationException("The server is already running");
        }

        var info = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add($"-Xms{Math.Min(memoryMb, 1024)}M");
        info.ArgumentList.Add($"-Xmx{memoryMb}M");
        info.ArgumentList.Add("-jar");
        info.ArgumentList.Add(jarFileName);
        info.ArgumentList.Add("nogui");

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Output?.Invoke(this, e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Output?.Invoke(this, e.Data);
        };

        process.Exited += (_, _) =>
        {
            Output?.Invoke(this, "Server stopped");
            RunningChanged?.Invoke(this, false);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        lock (_gate)
            _process = process;

        RunningChanged?.Invoke(this, true);
    }

    public void SendCommand(string command)
    {
        Process? process;

        lock (_gate)
            process = _process;

        if (process is null || process.HasExited)
            return;

        process.StandardInput.WriteLine(command);
        process.StandardInput.Flush();
        Output?.Invoke(this, "> " + command);
    }

    public async Task StopAsync(TimeSpan? timeout = null)
    {
        Process? process;

        lock (_gate)
            process = _process;

        if (process is null || process.HasExited)
            return;

        Output?.Invoke(this, "Stopping the server");

        try
        {
            process.StandardInput.WriteLine("stop");
            process.StandardInput.Flush();
        }
        catch (IOException)
        {
        }

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(45));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Output?.Invoke(this, "The server did not stop in time, terminating it");
            process.Kill(entireProcessTree: true);
        }
    }

    public async Task RestartAsync(
        string javaPath, string workingDirectory, string jarFileName, int memoryMb)
    {
        await StopAsync();
        Start(javaPath, workingDirectory, jarFileName, memoryMb);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            }

            _process?.Dispose();
            _process = null;
        }
    }
}
