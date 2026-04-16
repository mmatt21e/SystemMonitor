using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Engine.Capabilities;
using SystemMonitor.Engine.Collectors.Lhm;

namespace SystemMonitor.Engine.Collectors;

[SupportedOSPlatform("windows")]
public sealed class PowerCollector : CollectorBase
{
    private readonly LhmComputer? _lhm;

    public PowerCollector(TimeSpan pollingInterval, LhmComputer? lhm) : base("power", pollingInterval)
    {
        _lhm = lhm;
    }

    public override CapabilityStatus Capability =>
        _lhm is null
            ? CapabilityStatus.Unavailable("LibreHardwareMonitor not available (likely not admin)")
            : CapabilityStatus.Full();

    protected override IEnumerable<Reading> CollectCore()
    {
        if (_lhm is null) yield break;
        var ts = DateTimeOffset.UtcNow;

        foreach (var s in _lhm.EnumerateSensors())
        {
            if (!s.Value.HasValue) continue;

            var (metric, unit) = s.SensorType switch
            {
                SensorType.Voltage => ("voltage_volts", "V"),
                SensorType.Power   => ("power_watts", "W"),
                SensorType.Current => ("current_amps", "A"),
                SensorType.Fan     => ("fan_rpm", "RPM"),
                _ => (null, null)
            };
            if (metric is null) continue;

            yield return new Reading("power", metric, s.Value.Value, unit!, ts,
                ReadingConfidence.High,
                new Dictionary<string, string>
                {
                    ["sensor"] = s.Name,
                    ["hardware"] = s.Hardware.Name,
                    ["hardware_type"] = s.Hardware.HardwareType.ToString()
                });
        }

        // Battery / UPS state (laptops and connected UPSes).
        var power = System.Windows.Forms.SystemInformation.PowerStatus;
        yield return new Reading("power", "on_ac", power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? 1 : 0,
            "bool", ts, ReadingConfidence.High, new Dictionary<string, string>());
        yield return new Reading("power", "battery_percent", power.BatteryLifePercent * 100, "%", ts,
            ReadingConfidence.High, new Dictionary<string, string>());
    }
}
