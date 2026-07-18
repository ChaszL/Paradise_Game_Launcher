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

public class HardwareMonitorService
{
    private Computer _computer;

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        _computer.Open();
    }

    public HardwareInfo GetMetrics()
    {
        var info = new HardwareInfo();
        float memUsed = 0;
        float memAvailable = 0;

        foreach (IHardware hardware in _computer.Hardware)
        {
            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                // Handle CPU Load
                if (hardware.HardwareType == HardwareType.Cpu && 
                    sensor.SensorType == SensorType.Load && 
                    sensor.Name == "CPU Total")
                {
                    info.CPUValue = (int)(sensor.Value ?? 0);
                }

                // Handle GPU Load (AMD and NVIDIA)
                if ((hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia) && 
                    sensor.SensorType == SensorType.Load)
                {
                    // AMD often uses "GPU Core" while some drivers/versions might use "GPU Total"
                    if (sensor.Name == "GPU Core")
                    {
                        info.GPUValue = (int)(sensor.Value ?? 0);
                    }
                }

                // Handle RAM
                if (hardware.HardwareType == HardwareType.Memory)
                {
                    if (sensor.Name == "Memory Used") memUsed = sensor.Value ?? 0;
                    if (sensor.Name == "Memory Available") memAvailable = sensor.Value ?? 0;
                }
            }
        }

        // Calculate RAM percentage safely
        float totalRam = memUsed + memAvailable;
        if (totalRam > 0)
        {
            info.RAMValue = (int)((memUsed / totalRam) * 100);
        }

        return info;
    }
}