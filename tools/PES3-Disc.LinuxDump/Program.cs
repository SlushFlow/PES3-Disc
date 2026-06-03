using System.Text.Json;
using Ps3DiscDumper;
using Ps3DiscDumper.Utils;

namespace PES3Disc.LinuxDump;

/// <summary>
/// Linux-only retail PS3 disc dumper for PES3-Disc (separate from Windows pes3-disc-dump.exe).
/// Uses block devices (/dev/sr*) and FUSE mounts; not interchangeable with the Windows CLI.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static async Task<int> Main(string[] args)
    {
        PES3Disc.Core.PerformanceTuning.ApplyRuntimeDefaults();
        PES3Disc.Core.PerformanceTuning.TryBoostCurrentProcess();
        try
        {
            var options = LinuxCliOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(LinuxCliOptions.HelpText);
                return 0;
            }

            if (options.ProbeOnly)
                return await RunProbeAsync(options).ConfigureAwait(false);

            SettingsProvider.Settings = SettingsProvider.Settings with
            {
                OutputDir = options.OutputBase,
                IrdDir = options.IrdDir,
                CopyBdmv = false,
                CopyPs3Update = false,
                ShowDetails = false,
            };

            using var dumper = new Dumper();
            using var progressCts = new CancellationTokenSource();
            var progressTask = options.ProgressFile is { } pp
                ? WriteProgressLoopAsync(dumper, pp, progressCts.Token)
                : Task.CompletedTask;

            try
            {
                Emit("scan", "Scanning for PS3 disc (Linux)…");
                var inDir = options.ResolveInputDirectory();
                dumper.DetectDisc(inDir);

                if (string.IsNullOrEmpty(dumper.ProductCode))
                {
                    EmitError("no_disc", "No PS3 disc found. Mount the disc or pass --device /dev/sr0 (read access required).");
                    return 2;
                }

                Emit("key", $"Looking up decryption key for {dumper.ProductCode}…");
                await dumper.FindDiscKeyAsync(options.IrdDir).ConfigureAwait(false);

                Emit("dump", $"Decrypting to {options.OutputBase}…");
                await dumper.DumpAsync(options.OutputBase).ConfigureAwait(false);

                if (dumper.Cts.IsCancellationRequested)
                {
                    EmitError("cancelled", "Dump was cancelled.");
                    return 3;
                }

                if (dumper.ValidationStatus is false || dumper.BrokenFiles.Count > 0)
                {
                    EmitError("validation", $"Dump finished with {dumper.BrokenFiles.Count} invalid file(s).");
                    return 4;
                }

                var gameRoot = Path.Combine(options.OutputBase, dumper.OutputDir);
                var eboot = Path.Combine(gameRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN");
                if (!File.Exists(eboot))
                {
                    EmitError("missing_eboot", "Dump completed but EBOOT.BIN was not found.");
                    return 5;
                }

                Console.WriteLine(JsonSerializer.Serialize(new DumpResult
                {
                    Success = true,
                    ProductCode = dumper.ProductCode,
                    Title = dumper.Title,
                    GameRoot = gameRoot,
                    Eboot = eboot,
                }, JsonOptions));
                return 0;
            }
            finally
            {
                progressCts.Cancel();
                try { await progressTask.ConfigureAwait(false); } catch { /* ignore */ }
            }
        }
        catch (DriveNotFoundException ex)
        {
            EmitError("no_disc", ex.Message);
            return 2;
        }
        catch (KeyNotFoundException ex)
        {
            EmitError("no_key", ex.Message);
            return 6;
        }
        catch (AccessViolationException ex)
        {
            EmitError("access_denied", ex.Message);
            return 7;
        }
        catch (Exception ex)
        {
            EmitError("error", ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task WriteProgressLoopAsync(Dumper dumper, string path, CancellationToken token)
    {
        long lastSectors = -1;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var processed = dumper.ProcessedSectors + dumper.CurrentFileSector;
                if (processed != lastSectors || dumper.TotalFileSectors == 0)
                {
                    lastSectors = processed;
                    var progress = new DumpProgress
                    {
                        Phase = dumper.TotalFileSectors > 0 ? "dumping" : "analyzing",
                        CurrentFile = dumper.CurrentFileNumber,
                        TotalFiles = dumper.TotalFileCount,
                        ProcessedSectors = processed,
                        TotalFileSectors = dumper.TotalFileSectors,
                        ProductCode = dumper.ProductCode,
                        Title = dumper.Title,
                    };
                    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(progress, JsonOptions), token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore */ }

            try { await Task.Delay(250, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void Emit(string phase, string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new { type = "status", phase, message }, JsonOptions));

    private static void EmitError(string code, string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new { type = "error", code, message }, JsonOptions));

    private static async Task<int> RunProbeAsync(LinuxCliOptions options)
    {
        using var dumper = new Dumper();
        var inDir = options.ResolveInputDirectory();
        await Task.Run(() => dumper.DetectDisc(inDir)).ConfigureAwait(false);

        if (string.IsNullOrEmpty(dumper.ProductCode))
        {
            EmitError("no_disc", "No PS3 disc found.");
            return 2;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "probe",
            Success = true,
            ProductCode = dumper.ProductCode,
            Title = dumper.Title,
        }, JsonOptions));
        return 0;
    }
}

internal sealed class LinuxCliOptions
{
    public string? DevicePath { get; init; }
    public string? MountPath { get; init; }
    public required string OutputBase { get; init; }
    public required string IrdDir { get; init; }
    public string? ProgressFile { get; init; }
    public bool ProbeOnly { get; init; }
    public bool ShowHelp { get; init; }

    public const string HelpText = """
PES3-Disc Linux retail disc dumper

Separate from the Windows pes3-disc-dump tool. Uses /dev/sr* block devices and
mounted paths under /media or /run/media.

Usage:
  pes3-disc-dump-linux --output <folder> [--mount <path>] [--device /dev/sr0] [--ird-dir <dir>] [--progress <file>]
  pes3-disc-dump-linux --probe [--mount <path>] [--device /dev/sr0]

Options:
  --probe          Read disc product code only (fast; no decrypt)
  --output, -o     Output base folder (required unless --probe)
  --mount, -m      Mounted disc root (e.g. /run/media/user/PS3_DISC)
  --device         Preferred block device for sector decrypt (e.g. /dev/sr0)
  --ird-dir        IRD / key cache
  --progress, -p   Progress JSON file
  --help, -h       Show help

Requires read access to the optical drive (user in 'disk' group or root for raw device).
""";

    public string ResolveInputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(MountPath))
            return MountPath;
        return string.Empty;
    }

    public static LinuxCliOptions Parse(string[] args)
    {
        string? device = null;
        string? mount = null;
        string? output = null;
        string? irdDir = null;
        string? progress = null;
        var showHelp = false;
        var probeOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--probe":
                    probeOnly = true;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "-o":
                case "--output":
                    output = RequireValue(args, ref i, a);
                    break;
                case "-m":
                case "--mount":
                    mount = RequireValue(args, ref i, a);
                    break;
                case "--device":
                    device = RequireValue(args, ref i, a);
                    break;
                case "--ird-dir":
                    irdDir = RequireValue(args, ref i, a);
                    break;
                case "-p":
                case "--progress":
                    progress = RequireValue(args, ref i, a);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {a}");
            }
        }

        if (showHelp)
            return new LinuxCliOptions { ShowHelp = true, OutputBase = "", IrdDir = "" };

        if (!probeOnly && string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("Missing required --output.");

        irdDir ??= PES3Disc.Core.PlatformPaths.DefaultIrdDirectory;

        return new LinuxCliOptions
        {
            DevicePath = device,
            MountPath = mount is null ? null : Path.GetFullPath(mount),
            OutputBase = string.IsNullOrWhiteSpace(output) ? "" : Path.GetFullPath(output),
            IrdDir = Path.GetFullPath(irdDir),
            ProgressFile = progress,
            ProbeOnly = probeOnly,
        };
    }

    private static string RequireValue(string[] args, ref int i, string name)
    {
        if (++i >= args.Length)
            throw new ArgumentException($"Missing value for {name}.");
        return args[i];
    }
}

internal sealed class DumpResult
{
    public bool Success { get; set; }
    public string? ProductCode { get; set; }
    public string? Title { get; set; }
    public string? GameRoot { get; set; }
    public string? Eboot { get; set; }
}

internal sealed class DumpProgress
{
    public string? Phase { get; set; }
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public long ProcessedSectors { get; set; }
    public long TotalFileSectors { get; set; }
    public string? ProductCode { get; set; }
    public string? Title { get; set; }
}
