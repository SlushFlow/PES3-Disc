using System.Text.Json;
using Ps3DiscDumper;
using Ps3DiscDumper.Utils;

namespace PES3Disc.DumpCli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(CliOptions.HelpText);
                return 0;
            }

            SettingsProvider.Settings = SettingsProvider.Settings with
            {
                OutputDir = options.OutputBase,
                IrdDir = options.IrdDir,
                CopyBdmv = false,
                CopyPs3Update = false,
            };

            using var dumper = new Dumper();
            var progressPath = options.ProgressFile;
            using var progressCts = new CancellationTokenSource();
            var progressTask = progressPath is not null
                ? WriteProgressLoopAsync(dumper, progressPath, progressCts.Token)
                : Task.CompletedTask;

            try
            {
                Emit("scan", "Scanning for PS3 disc…");
                var inDir = options.MountPath ?? (options.DriveLetter.HasValue
                    ? $"{options.DriveLetter.Value}:\\"
                    : string.Empty);
                dumper.DetectDisc(inDir);

                if (string.IsNullOrEmpty(dumper.ProductCode))
                {
                    EmitError("no_disc", "No PS3 disc found. The drive must expose PS3_DISC.SFB (compatible Blu-ray drive required).");
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

                var result = new DumpResult
                {
                    Success = true,
                    ProductCode = dumper.ProductCode,
                    Title = dumper.Title,
                    GameRoot = gameRoot,
                    Eboot = eboot,
                };
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                var progress = new DumpProgress
                {
                    Phase = dumper.TotalFileSectors > 0 ? "dumping" : "analyzing",
                    CurrentFile = dumper.CurrentFileNumber,
                    TotalFiles = dumper.TotalFileCount,
                    ProcessedSectors = dumper.ProcessedSectors + dumper.CurrentFileSector,
                    TotalFileSectors = dumper.TotalFileSectors,
                    ProductCode = dumper.ProductCode,
                    Title = dumper.Title,
                };
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(progress, JsonOptions), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore write races */ }

            try { await Task.Delay(400, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void Emit(string phase, string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new { type = "status", phase, message }, JsonOptions));

    private static void EmitError(string code, string message) =>
        Console.WriteLine(JsonSerializer.Serialize(new { type = "error", code, message }, JsonOptions));
}

internal sealed class CliOptions
{
    public char? DriveLetter { get; init; }
    public string? MountPath { get; init; }
    public required string OutputBase { get; init; }
    public required string IrdDir { get; init; }
    public string? ProgressFile { get; init; }
    public bool ShowHelp { get; init; }

    public const string HelpText = """
PES3-Disc retail disc decryptor (uses PS3 Disc Dumper engine)

Usage:
  pes3-disc-dump --output <folder> [--drive <letter> | --mount <path>] [--ird-dir <folder>] [--progress <file.json>]

Options:
  --output, -o    Base folder for decrypted dump (required)
  --drive, -d     Windows optical drive letter (e.g. E)
  --mount, -m     Mounted disc path (Linux: /run/media/user/… or /media/…)
  --ird-dir       IRD / key cache folder
  --progress, -p  Write progress JSON to this file while dumping
  --help, -h      Show help

Exit codes: 0=ok, 1=error, 2=no disc, 3=cancelled, 4=validation, 5=no eboot, 6=no key, 7=access denied
""";

    public static CliOptions Parse(string[] args)
    {
        char? drive = null;
        string? mount = null;
        string? output = null;
        string? irdDir = null;
        string? progress = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "-o":
                case "--output":
                    output = RequireValue(args, ref i, a);
                    break;
                case "-d":
                case "--drive":
                    var d = RequireValue(args, ref i, a);
                    if (d.Length is not 1) throw new ArgumentException("Drive letter must be a single character.");
                    drive = char.ToUpperInvariant(d[0]);
                    break;
                case "-m":
                case "--mount":
                    mount = RequireValue(args, ref i, a);
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
            return new CliOptions { ShowHelp = true, OutputBase = "", IrdDir = "" };

        if (string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("Missing required --output folder.");

        irdDir ??= PES3Disc.Core.PlatformPaths.DefaultIrdDirectory;

        return new CliOptions
        {
            DriveLetter = drive,
            MountPath = mount is null ? null : Path.GetFullPath(mount),
            OutputBase = Path.GetFullPath(output),
            IrdDir = Path.GetFullPath(irdDir),
            ProgressFile = progress,
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
