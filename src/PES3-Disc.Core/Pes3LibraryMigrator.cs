namespace PES3Disc.Core;

/// <summary>Moves legacy flat <c>PES3/cache</c> title folders into <c>PES3/library/titles</c>.</summary>
public static class Pes3LibraryMigrator
{
    private const string MarkerFile = ".pes3-library-migration-v1.done";

    public static void MigrateIfNeeded(Pes3Paths paths)
    {
        var libraryRoot = paths.LibraryRoot;
        var marker = Path.Combine(libraryRoot, MarkerFile);
        if (File.Exists(marker))
            return;

        var legacyRoot = paths.LegacyCacheRoot;
        var titlesRoot = paths.LibraryTitlesRoot;
        Directory.CreateDirectory(titlesRoot);

        var index = Pes3LibraryIndex.Load(libraryRoot);
        index.ImportLegacyCacheIndex(Pes3CacheIndex.Load(legacyRoot), libraryRoot);

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pes3-cache-index.json",
            MarkerFile,
            Path.GetFileName(Pes3LibraryIndex.IndexPath(libraryRoot)),
        };

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(legacyRoot))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name) || reserved.Contains(name))
                    continue;

                var target = Pes3Paths.TitleInstallPath(libraryRoot, name);
                if (Directory.Exists(target))
                    continue;

                try
                {
                    Directory.Move(dir, target);
                    index.Upsert(name, target, null, Pes3LibraryTier.PersistentLibrary);
                    Pes3Log.Write($"Library migration: {name} → {target}");
                }
                catch (Exception ex)
                {
                    Pes3Log.Write($"Library migration skipped {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Library migration scan failed: {ex.Message}");
        }

        index.Save(libraryRoot);
        try
        {
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // ignore
        }
    }
}
