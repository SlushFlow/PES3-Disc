using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace PES3Disc.Core;

public abstract class DiscDumpBackendBase : IDiscDumpBackend
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    protected readonly Pes3Config? Config;

    protected DiscDumpBackendBase(Pes3Config? config) => Config = config;

    public abstract bool IsAvailable { get; }

    public string? ExecutablePath => FindDumpCliPath();

    protected abstract string? FindDumpCliPath();
    protected abstract string BuildArguments(OpticalDrive drive, string outputBase, string progressFile);
    protected abstract string BuildProbeArguments(OpticalDrive drive);

    public async Task<DiscProbeResult?> ProbeDiscAsync(
        OpticalDrive drive,
        CancellationToken cancellationToken = default)
    {
        var cli = FindDumpCliPath();
        if (cli is null)
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cli,
                Arguments = BuildProbeArguments(drive),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            PerformanceTuning.TryBoostChildProcess(proc);
            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (proc.ExitCode != 0)
                return null;

            foreach (var line in stdout.Split('\n'))
            {
                if (!line.Contains("\"type\"", StringComparison.OrdinalIgnoreCase) ||
                    !line.Contains("probe", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    var probe = JsonSerializer.Deserialize<DumpCliProbe>(line.Trim(), JsonOptions);
                    if (!string.IsNullOrEmpty(probe?.ProductCode))
                    {
                        return new DiscProbeResult
                        {
                            ProductCode = probe.ProductCode,
                            Title = probe.Title,
                        };
                    }
                }
                catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Disc probe failed: {ex.Message}");
        }

        return null;
    }

    public async Task<DecryptResult> DecryptAsync(
        OpticalDrive drive,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        PerformanceTuning.ApplyRuntimeDefaults();
        PerformanceTuning.TryBoostCurrentProcess();

        var cli = FindDumpCliPath();
        if (cli is null)
        {
            return Fail("no_cli", OperatingSystem.IsLinux()
                ? "pes3-disc-dump-linux not found. Reinstall the Linux bundle or set DumpCliPath."
                : "pes3-disc-dump.exe not found. Run Build-App.ps1 or Setup.ps1 -RetailDecrypt.");
        }

        Directory.CreateDirectory(outputBase);
        var progressFile = Path.Combine(Path.GetTempPath(), "pes3-disc-progress-" + Guid.NewGuid().ToString("N") + ".json");
        var args = BuildArguments(drive, outputBase, progressFile);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cli,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment["DOTNET_gcServer"] = "1";
            psi.Environment["DOTNET_ReadyToRun"] = "1";

            var proc = Process.Start(psi);
            if (proc is null)
                return Fail("error", "Could not start dump process.");

            PerformanceTuning.TryBoostChildProcess(proc);

            using var cancelReg = cancellationToken.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            });

            try
            {
                var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var progressTask = PollProgressAsync(progressFile, progress, progressCts.Token);
                var stdoutTask = DrainStreamAsync(proc.StandardOutput, cancellationToken);
                var stderrTask = DrainStreamAsync(proc.StandardError, cancellationToken);

                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                await progressCts.CancelAsync().ConfigureAwait(false);
                try { await progressTask.ConfigureAwait(false); } catch { /* ignore */ }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);

                if (proc.ExitCode != 0)
                {
                    var err = ParseLastError(stdout + "\n" + stderr)
                        ?? (string.IsNullOrWhiteSpace(stderr) ? $"Decryption failed (exit {proc.ExitCode})." : stderr.Trim());
                    return Fail("dump_failed", err);
                }

                foreach (var line in stdout.Split('\n'))
                {
                    if (!line.Contains("\"Success\"", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        var result = JsonSerializer.Deserialize<DumpCliResult>(line.Trim(), JsonOptions);
                        if (result?.Success == true && !string.IsNullOrEmpty(result.Eboot))
                        {
                            return new DecryptResult
                            {
                                Success = true,
                                ProductCode = result.ProductCode,
                                Title = result.Title,
                                GameRoot = result.GameRoot,
                                Eboot = result.Eboot,
                            };
                        }
                    }
                    catch { /* try next line */ }
                }

                return Fail("missing_eboot", "Dump finished but success JSON was not found in output.");
            }
            finally
            {
                proc.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            return Fail("cancelled", "Decryption was cancelled.");
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Decrypt error: {ex}");
            return Fail("error", ex.Message);
        }
        finally
        {
            try { File.Delete(progressFile); } catch { /* ignore */ }
        }
    }

    private static async Task<string> DrainStreamAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return "";
        }
    }

    private static async Task PollProgressAsync(
        string progressFile,
        IProgress<DecryptProgress>? progress,
        CancellationToken cancellationToken)
    {
        long lastSectors = -1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(progressFile))
            {
                try
                {
                    await using var fs = new FileStream(
                        progressFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 4096,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    using var sr = new StreamReader(fs);
                    var json = await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var p = JsonSerializer.Deserialize<DumpCliProgress>(json, JsonOptions);
                    if (p is not null && p.ProcessedSectors != lastSectors)
                    {
                        lastSectors = p.ProcessedSectors;
                        progress?.Report(new DecryptProgress
                        {
                            Phase = p.Phase ?? (p.TotalFileSectors > 0 ? "dumping" : "analyzing"),
                            CurrentFile = p.CurrentFile,
                            TotalFiles = p.TotalFiles,
                            ProcessedSectors = p.ProcessedSectors,
                            TotalFileSectors = p.TotalFileSectors,
                            ProductCode = p.ProductCode,
                            Title = p.Title,
                        });
                    }
                }
                catch { /* ignore */ }
            }
            try { await Task.Delay(250, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static string? ParseLastError(string stdout)
    {
        foreach (var line in Enumerable.Reverse(stdout.Split('\n')))
        {
            if (!line.Contains("\"type\"", StringComparison.OrdinalIgnoreCase) ||
                !line.Contains("error", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var err = JsonSerializer.Deserialize<DumpCliError>(line.Trim(), JsonOptions);
                if (!string.IsNullOrEmpty(err?.Message))
                    return err.Message;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    protected static DecryptResult Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };

    private sealed class DumpCliResult
    {
        public bool Success { get; set; }
        public string? ProductCode { get; set; }
        public string? Title { get; set; }
        public string? GameRoot { get; set; }
        public string? Eboot { get; set; }
    }

    private sealed class DumpCliProgress
    {
        public string? Phase { get; set; }
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public long ProcessedSectors { get; set; }
        public long TotalFileSectors { get; set; }
        public string? ProductCode { get; set; }
        public string? Title { get; set; }
    }

    private sealed class DumpCliError
    {
        public string? Message { get; set; }
    }

    private sealed class DumpCliProbe
    {
        public string? ProductCode { get; set; }
        public string? Title { get; set; }
    }
}
