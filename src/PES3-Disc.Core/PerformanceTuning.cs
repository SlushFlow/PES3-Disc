using System.Diagnostics;
using System.Runtime;

namespace PES3Disc.Core;

/// <summary>Shared performance helpers for decrypt and staging.</summary>
public static class PerformanceTuning
{
    public const int FileCopyBufferBytes = 1024 * 1024;
    public const int RobocopyThreads = 16;

    public static void ApplyRuntimeDefaults()
    {
        try
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        }
        catch
        {
            // ignore
        }
    }

    public static void TryBoostCurrentProcess()
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            if (OperatingSystem.IsWindows())
                p.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch
        {
            // ignore
        }
    }

    public static void TryBoostChildProcess(Process? proc)
    {
        if (proc is null)
            return;
        try
        {
            if (OperatingSystem.IsWindows())
                proc.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch
        {
            // ignore
        }
    }
}
