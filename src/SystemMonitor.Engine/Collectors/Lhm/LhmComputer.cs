using System.Runtime.Versioning;
using LibreHardwareMonitor.Hardware;

namespace SystemMonitor.Engine.Collectors.Lhm;

/// <summary>
/// Shared wrapper around a LibreHardwareMonitor <see cref="Computer"/> instance. Opened once
/// at startup and reused by all LHM-backed collectors (Power, GPU, CPU temps). Each collector
/// selects the subset of sensors it cares about via <see cref="EnumerateSensors"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LhmComputer : IDisposable
{
    private readonly Computer _computer;

    private LhmComputer(Computer computer) => _computer = computer;

    public static LhmComputer Open()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false,        // we cover this via OS APIs
            IsControllerEnabled = false,
            IsBatteryEnabled = true,
            IsPsuEnabled = true
        };
        computer.Open();
        return new LhmComputer(computer);
    }

    /// <summary>
    /// Traverses all hardware and returns every sensor. Callers filter by
    /// <see cref="ISensor.HardwareType"/> and <see cref="ISensor.SensorType"/>.
    /// </summary>
    public IEnumerable<ISensor> EnumerateSensors()
    {
        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
            {
                sub.Update();
                foreach (var s in sub.Sensors) yield return s;
            }
            foreach (var s in hw.Sensors) yield return s;
        }
    }

    public void Dispose() => _computer.Close();
}
