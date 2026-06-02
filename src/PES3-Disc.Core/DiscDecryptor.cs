using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace PES3Disc.Core;

/// <summary>
/// Runs retail disc decryption via pes3-disc-dump.exe (same engine as the CLI project).
/// </summary>
public sealed class DiscDecryptor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string? FindDumpCliPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "pes3-disc-dump.exe"),
            Path.Combine(baseDir, "tools", "pes3-disc-dump.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tools", "pes3-disc-dump.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "tools", "pes3-disc-dump.exe")),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public bool IsAvailable => FindDumpCliPath() is not null;

    public async Task<DecryptResult> DecryptAsync(
        char driveLetter,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var cli = FindDumpCliPath();
        if (cli is null)
        {
            return new DecryptResult
            {
                Success = false,
                ErrorCode = "no_cli",
                ErrorMessage = "pes3-disc-dump.exe not found. Run Build-App.ps1 or Setup.ps1 -RetailDecrypt.",
            };
        }

        Directory.CreateDirectory(outputBase);
        var progressFile = Path.Combine(Path.GetTempPath(), "pes3-disc-progress-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cli,
                Arguments = $"--output \"{outputBase}\" --drive {driveLetter} --progress \"{progressFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return Fail("error", "Could not start pes3-disc-dump.exe.");
            }

            var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressTask = PollProgressAsync(progressFile, progress, progressCts.Token);
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

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
                catch
                {
                    // try next line
                }
            }

            return Fail("missing_eboot", "Decrypt finished but success JSON was not found in output.");
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

    private static async Task PollProgressAsync(
        string progressFile,
        IProgress<DecryptProgress>? progress,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(progressFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(progressFile, cancellationToken).ConfigureAwait(false);
                    var p = JsonSerializer.Deserialize<DumpCliProgress>(json, JsonOptions);
                    if (p is not null)
                    {
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
                catch
                {
                    // ignore read races
                }
            }

            try { await Task.Delay(400, cancellationToken).ConfigureAwait(false); }
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

    private static DecryptResult Fail(string code, string message) =>
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
}
