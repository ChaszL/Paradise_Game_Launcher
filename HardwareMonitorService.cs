using LibreHardwareMonitor.Hardware;

namespace GameLauncher;

public class HardwareInfo
{
    // Raw Percentages (0-100)
    public int CPUValue { get; set; } = 0;
    public int GPUValue { get; set; } = 0;
    public int RAMValue { get; set; } = 0;

    // String Display (Read-Only)
    public string CPUUsage => $"{CPUValue}%";
    public string GPUUsage => $"{GPUValue}%";
    public string RAMUsage => $"{RAMValue}%";

    // Dynamic Colors based on capacity (Read-Only)
    public string CPUColor => GetColor(CPUValue);
    public string GPUColor => GetColor(GPUValue);
    public string RAMColor => GetColor(RAMValue);

    private string GetColor(int value)
    {
        if (value >= 80) return "#FF4444"; // Red
        if (value >= 50) return "#FFD700"; // Yellow/Gold
        return "#00FF7F"; // Spring Green
    }
}

public class HardwareMonitorService : IDisposable
{
    private Computer? _computer;
    private bool _available;

    public HardwareMonitorService()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true
            };
            _computer.Open();
            _available = true;
        }
        catch
        {
            // Sensor access failed (no admin rights, unsupported hardware, VM, etc.)
            // Leave _available false so GetMetrics() returns safe zeroed data instead of crashing.
            _computer = null;
            _available = false;
        }
    }

    public HardwareInfo GetMetrics()
    {
        var info = new HardwareInfo();
        if (!_available || _computer == null) return info; // all zeros, UI just shows 0%

        float memUsed = 0;
        float memAvailable = 0;

        try
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (hardware.HardwareType == HardwareType.Cpu &&
                        sensor.SensorType == SensorType.Load &&
                        sensor.Name == "CPU Total")
                    {
                        info.CPUValue = (int)(sensor.Value ?? 0);
                    }

                    if ((hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia) &&
                        sensor.SensorType == SensorType.Load)
                    {
                        if (sensor.Name == "GPU Core")
                        {
                            info.GPUValue = (int)(sensor.Value ?? 0);
                        }
                    }

                    if (hardware.HardwareType == HardwareType.Memory)
                    {
                        if (sensor.Name == "Memory Used") memUsed = sensor.Value ?? 0;
                        if (sensor.Name == "Memory Available") memAvailable = sensor.Value ?? 0;
                    }
                }
            }

            float totalRam = memUsed + memAvailable;
            if (totalRam > 0)
            {
                info.RAMValue = (int)((memUsed / totalRam) * 100);
            }
        }
        catch
        {
            // A sensor read failed mid-poll (e.g. device disconnected). Return whatever we got.
        }

        return info;
    }

    public void Dispose()
    {
        try { _computer?.Close(); } catch { }
        _computer = null;
    }
}