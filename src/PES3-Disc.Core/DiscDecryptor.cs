using Ps3DiscDumper;
using Ps3DiscDumper.Utils;

namespace PES3Disc.Core;

public sealed class DiscDecryptor
{
    public async Task<DecryptResult> DecryptAsync(
        char driveLetter,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SettingsProvider.Settings = SettingsProvider.Settings with
        {
            OutputDir = outputBase,
            CopyBdmv = false,
            CopyPs3Update = false,
        };

        using var dumper = new Dumper();
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(400));

        var progressTask = Task.Run(async () =>
        {
            while (await timer.WaitForNextTickAsync(progressCts.Token).ConfigureAwait(false))
            {
                progress?.Report(new DecryptProgress
                {
                    Phase = dumper.TotalFileSectors > 0 ? "dumping" : "analyzing",
                    CurrentFile = dumper.CurrentFileNumber,
                    TotalFiles = dumper.TotalFileCount,
                    ProcessedSectors = dumper.ProcessedSectors + dumper.CurrentFileSector,
                    TotalFileSectors = dumper.TotalFileSectors,
                    ProductCode = dumper.ProductCode,
                    Title = dumper.Title,
                });
            }
        }, progressCts.Token);

        try
        {
            var inDir = $"{driveLetter}:\\";
            dumper.DetectDisc(inDir);

            if (string.IsNullOrEmpty(dumper.ProductCode))
            {
                return Fail("no_disc", "No PS3 disc found. Use a compatible Blu-ray drive.");
            }

            progress?.Report(new DecryptProgress
            {
                Phase = "key",
                Title = dumper.Title,
                ProductCode = dumper.ProductCode,
            });

            var irdDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ps3-disc-dumper", "ird");
            Directory.CreateDirectory(irdDir);
            await dumper.FindDiscKeyAsync(irdDir).ConfigureAwait(false);

            progress?.Report(new DecryptProgress { Phase = "dump", Title = dumper.Title, ProductCode = dumper.ProductCode });
            await dumper.DumpAsync(outputBase).ConfigureAwait(false);

            if (dumper.Cts.IsCancellationRequested)
                return Fail("cancelled", "Decryption was cancelled.");

            if (dumper.ValidationStatus is false || dumper.BrokenFiles.Count > 0)
                return Fail("validation", $"Dump finished with {dumper.BrokenFiles.Count} invalid file(s).");

            var gameRoot = Path.Combine(outputBase, dumper.OutputDir);
            var eboot = Path.Combine(gameRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN");
            if (!File.Exists(eboot))
                return Fail("missing_eboot", "Decrypt completed but EBOOT.BIN was not found.");

            return new DecryptResult
            {
                Success = true,
                ProductCode = dumper.ProductCode,
                Title = dumper.Title,
                GameRoot = gameRoot,
                Eboot = eboot,
            };
        }
        catch (DriveNotFoundException ex)
        {
            return Fail("no_disc", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Fail("no_key", ex.Message);
        }
        catch (AccessViolationException ex)
        {
            return Fail("access_denied", ex.Message);
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
            await progressCts.CancelAsync().ConfigureAwait(false);
            try { await progressTask.ConfigureAwait(false); } catch { /* ignore */ }
        }
    }

    private static DecryptResult Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
