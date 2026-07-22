using System.Net;
using Mono.Nat;

namespace Vesper.Core.Servers;

public enum PortForwardOutcome
{
    Mapped,
    NoRouterFound,
    RouterRefused,
    BehindCarrierNat,
}

public sealed record PortForwardResult(
    PortForwardOutcome Outcome,
    string? ExternalAddress,
    int Port,
    string Message)
{
    public bool Success => Outcome == PortForwardOutcome.Mapped;

    public string? ShareableAddress =>
        Success && ExternalAddress is not null ? $"{ExternalAddress}:{Port}" : null;
}

public sealed class PortForwarding
{
    public const string MappingDescription = "Vesper Launcher";

    private readonly TimeSpan _discoveryTimeout;
    private INatDevice? _device;

    public PortForwarding(TimeSpan? discoveryTimeout = null) =>
        _discoveryTimeout = discoveryTimeout ?? TimeSpan.FromSeconds(8);

    public async Task<INatDevice?> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        if (_device is not null)
            return _device;

        var completion = new TaskCompletionSource<INatDevice?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFound(object? sender, DeviceEventArgs args) =>
            completion.TrySetResult(args.Device);

        NatUtility.DeviceFound += OnFound;

        try
        {
            NatUtility.StartDiscovery(NatProtocol.Upnp, NatProtocol.Pmp);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_discoveryTimeout);

            using (timeout.Token.Register(() => completion.TrySetResult(null)))
            {
                _device = await completion.Task;
            }
        }
        catch (Exception)
        {
            _device = null;
        }
        finally
        {
            NatUtility.DeviceFound -= OnFound;
            NatUtility.StopDiscovery();
        }

        return _device;
    }

    public async Task<PortForwardResult> OpenAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        var device = await DiscoverAsync(cancellationToken);

        if (device is null)
        {
            return new PortForwardResult(
                PortForwardOutcome.NoRouterFound, null, port,
                "No router responded to UPnP. It is usually turned off by default, so enable " +
                "UPnP or NAT-PMP in your router settings and try again.");
        }

        try
        {
            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, port, port, 0, MappingDescription));
            await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, 0, MappingDescription));
        }
        catch (Exception e)
        {
            return new PortForwardResult(
                PortForwardOutcome.RouterRefused, null, port,
                "The router refused to open the port: " + e.Message);
        }

        IPAddress? external = null;

        try
        {
            external = await device.GetExternalIPAsync();
        }
        catch (Exception)
        {
            external = null;
        }

        if (external is not null && IsUnreachable(external))
        {
            return new PortForwardResult(
                PortForwardOutcome.BehindCarrierNat, external.ToString(), port,
                $"The port was opened, but your router's public address ({external}) is itself " +
                "behind your provider's network. Port forwarding cannot reach you from the " +
                "internet on this connection. Ask your provider for a public address, or use a tunnel.");
        }

        return new PortForwardResult(
            PortForwardOutcome.Mapped, external?.ToString(), port,
            external is null
                ? "Port " + port + " is open. The router did not report a public address."
                : $"Port {port} is open. Friends can join at {external}:{port}.");
    }

    public async Task CloseAsync(int port, CancellationToken cancellationToken = default)
    {
        var device = await DiscoverAsync(cancellationToken);

        if (device is null)
            return;

        foreach (var protocol in new[] { Protocol.Tcp, Protocol.Udp })
        {
            try
            {
                await device.DeletePortMapAsync(new Mapping(protocol, port, port, 0, MappingDescription));
            }
            catch (Exception)
            {
            }
        }
    }

    public static bool IsUnreachable(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (bytes.Length != 4)
            return false;

        return bytes[0] switch
        {
            10 => true,
            127 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            169 when bytes[1] == 254 => true,
            100 when bytes[1] >= 64 && bytes[1] <= 127 => true,
            _ => false,
        };
    }
}
