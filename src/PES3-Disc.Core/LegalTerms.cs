using System.Diagnostics;

namespace PES3Disc.Core;

/// <summary>Versioned in-app legal acknowledgments (not legal advice).</summary>
public static class LegalTerms
{
    /// <summary>Bump when LEGAL.md / required acknowledgments change materially.</summary>
    public const string CurrentVersion = "2026-06-02";

    public static readonly string[] RequiredAcknowledgments =
    [
        "I own the physical PS3 disc I use with decrypt or copy features.",
        "I will not share, upload, or redistribute decrypted or copied game files.",
        "I understand PES3-Disc does not grant copyright or circumvention rights; I follow laws in my country.",
    ];

    public static bool IsAccepted(Pes3Config config) =>
        string.Equals(config.AcceptedLegalTermsVersion, CurrentVersion, StringComparison.Ordinal);

    public static void RecordAcceptance(Pes3Config config)
    {
        config.AcceptedLegalTermsVersion = CurrentVersion;
        config.LegalTermsAcceptedUtc = DateTime.UtcNow;
    }

    public static void ClearAcceptance(Pes3Config config)
    {
        config.AcceptedLegalTermsVersion = null;
        config.LegalTermsAcceptedUtc = null;
    }

    public static string? ResolveDocumentPath(string fileName)
    {
        var bases = new[]
        {
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..")),
        };

        foreach (var root in bases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var rel in new[] { fileName, Path.Combine("docs", fileName) })
            {
                var path = Path.Combine(root, rel);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    public static void TryOpenDocument(string fileName)
    {
        var path = ResolveDocumentPath(fileName);
        if (path is null)
        {
            Pes3Log.Write($"Legal document not found: {fileName}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Could not open {fileName}: {ex.Message}");
        }
    }

    public const string CliNotice = """
        PES3-Disc — legal notice (not legal advice)
        - You must OWN the physical disc you decrypt or copy.
        - Do NOT redistribute decrypted or copied game files.
        - Circumvention and copying laws vary by country; you are responsible for compliance.
        - Full terms: LEGAL.md next to this program.

        """;
}
