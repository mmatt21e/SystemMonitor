using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using SystemMonitor.Engine.Capabilities;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class NetworkCollector : CollectorBase, IDisposable
{
    public NetworkCollector(TimeSpan pollingInterval) : base("network", pollingInterval) { }

    public override CapabilityStatus Capability => CapabilityStatus.Full();

    public void Dispose() { /* nothing to release: NetworkInterface is static, Ping is using-scoped */ }

    protected override IEnumerable<Reading> CollectCore()
    {
        var ts = DateTimeOffset.UtcNow;
        var results = new List<Reading>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            var labels = new Dictionary<string, string>
            {
                ["adapter"] = nic.Name,
                ["type"] = nic.NetworkInterfaceType.ToString()
            };

            results.Add(new Reading("network", "link_up",
                nic.OperationalStatus == OperationalStatus.Up ? 1 : 0,
                "bool", ts, ReadingConfidence.High, labels));

            try
            {
                var stats = nic.GetIPv4Statistics();
                results.Add(new Reading("network", "incoming_packet_errors",
                    stats.IncomingPacketsWithErrors, "count", ts, ReadingConfidence.High, labels));
                results.Add(new Reading("network", "outgoing_packet_errors",
                    stats.OutgoingPacketsWithErrors, "count", ts, ReadingConfidence.High, labels));
                results.Add(new Reading("network", "incoming_discards",
                    stats.IncomingPacketsDiscarded, "count", ts, ReadingConfidence.High, labels));
            }
            catch { /* some virtual adapters don't expose IPv4 stats — skip quietly */ }
        }

        results.Add(PingGateway(ts));
        return results;
    }

    private static Reading PingGateway(DateTimeOffset ts)
    {
        var labels = new Dictionary<string, string> { ["target"] = "gateway" };
        try
        {
            var gateway = GetDefaultGatewayAddress();
            if (gateway is null)
                return new Reading("network", "gateway_latency_ms", -1, "ms", ts, ReadingConfidence.Low, labels);

            using var ping = new Ping();
            var reply = ping.Send(gateway, 1000);
            var value = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            labels["target_ip"] = gateway;
            return new Reading("network", "gateway_latency_ms", value, "ms", ts, ReadingConfidence.High, labels);
        }
        catch
        {
            return new Reading("network", "gateway_latency_ms", -1, "ms", ts, ReadingConfidence.Low, labels);
        }
    }

    private static string? GetDefaultGatewayAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var gw in nic.GetIPProperties().GatewayAddresses)
            {
                if (gw.Address is null) continue;
                var s = gw.Address.ToString();
                if (s != "0.0.0.0" && !string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }
}
