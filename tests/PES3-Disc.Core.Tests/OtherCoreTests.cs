using PES3Disc.BugReports;
using PES3Disc.Core;
using PES3Disc.Core.Tests.Fixtures;

namespace PES3Disc.Core.Tests;

public class DevStatusLogicTests
{
    private static DateTime UtcFromEastern(int year, int month, int day, int hour, int minute = 30)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                return TimeZoneInfo.ConvertTimeToUtc(local, tz);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return local.AddHours(5);
    }

    [Fact]
    public void Auto_green_during_work_hours()
    {
        var utc = UtcFromEastern(2026, 1, 15, 8);
        Assert.Equal(DevStatusKind.Green, DevStatusLogic.ResolveEffective("auto", utc));
    }

    [Fact]
    public void Auto_grey_after_hours()
    {
        var utc = UtcFromEastern(2026, 1, 14, 22);
        Assert.Equal(DevStatusKind.Grey, DevStatusLogic.ResolveEffective("auto", utc));
    }

    [Fact]
    public void Manual_yellow_overrides_schedule()
    {
        var utc = UtcFromEastern(2026, 1, 15, 8);
        Assert.Equal(DevStatusKind.Yellow, DevStatusLogic.ResolveEffective("yellow", utc));
    }

    [Fact]
    public void Manual_green_overrides_schedule()
    {
        var utc = UtcFromEastern(2026, 1, 14, 22);
        Assert.Equal(DevStatusKind.Green, DevStatusLogic.ResolveEffective("green", utc));
    }

    [Fact]
    public void Manual_grey_overrides_schedule()
    {
        var utc = UtcFromEastern(2026, 1, 15, 8);
        Assert.Equal(DevStatusKind.Grey, DevStatusLogic.ResolveEffective("grey", utc));
    }

    [Fact]
    public void GetDelayUntilNextBoundary_is_positive_and_bounded()
    {
        var delay = DevStatusLogic.GetDelayUntilNextBoundary(DateTime.UtcNow);
        Assert.InRange(delay.TotalSeconds, 1, 86400);
    }

    [Fact]
    public void BuildDisplay_auto_includes_label()
    {
        var display = DevStatusLogic.BuildDisplay("auto");
        Assert.False(string.IsNullOrWhiteSpace(display.Label));
        Assert.True(display.IsAutoSchedule);
    }

    [Fact]
    public void Invalid_manual_mode_falls_back_to_schedule()
    {
        var utc = UtcFromEastern(2026, 6, 15, 11);
        var kind = DevStatusLogic.ResolveEffective("not-a-color", utc);
        Assert.Equal(DevStatusKind.Green, kind);
    }
}

public class BugReportLimitsTests
{
    [Fact]
    public void Validate_accepts_good_report()
    {
        var (title, body) = BugReportLimits.Validate("Crash on scan", "Steps to reproduce...");
        Assert.Equal("Crash on scan", title);
        Assert.Equal("Steps to reproduce...", body);
    }

    [Fact]
    public void Validate_rejects_empty_title()
    {
        Assert.Throws<ArgumentException>(() => BugReportLimits.Validate("", "body"));
    }

    [Fact]
    public void Validate_rejects_title_too_long()
    {
        var longTitle = new string('x', BugReportLimits.MaxTitleLength + 1);
        Assert.Throws<ArgumentException>(() => BugReportLimits.Validate(longTitle, "body"));
    }

    [Fact]
    public void Validate_rejects_body_too_long()
    {
        var longBody = new string('x', BugReportLimits.MaxBodyLength + 1);
        Assert.Throws<ArgumentException>(() => BugReportLimits.Validate("title", longBody));
    }

    [Fact]
    public void ValidateResolutionMessage_rejects_too_long()
    {
        var msg = new string('x', BugReportLimits.MaxResolutionMessageLength + 1);
        Assert.Throws<ArgumentException>(() => BugReportLimits.ValidateResolutionMessage(msg));
    }
}

public class ReportClusteringTests
{
    [Fact]
    public void Similar_reports_cluster()
    {
        var score = ReportClustering.Similarity(
            "Scan crash when inserting disc",
            "When I scan the disc the program crashes",
            "Scan crash",
            "App crashes when I scan the disc");
        Assert.True(score >= BugReportLimits.ClusterSimilarityThreshold);
    }

    [Fact]
    public void Unrelated_reports_do_not_cluster()
    {
        var score = ReportClustering.Similarity(
            "Backup failed",
            "Restore button missing",
            "Decrypt slow",
            "Linux mount path wrong");
        Assert.True(score < BugReportLimits.ClusterSimilarityThreshold);
    }
}

public class LegalTermsTests
{
    [Fact]
    public void Acceptance_round_trip()
    {
        var config = new Pes3Config();
        Assert.False(LegalTerms.IsAccepted(config));
        LegalTerms.RecordAcceptance(config);
        Assert.True(LegalTerms.IsAccepted(config));
        LegalTerms.ClearAcceptance(config);
        Assert.False(LegalTerms.IsAccepted(config));
    }

    [Fact]
    public void Stale_acceptance_not_valid()
    {
        var config = new Pes3Config { AcceptedLegalTermsVersion = "2020-01-01" };
        Assert.False(LegalTerms.IsAccepted(config));
    }
}

public class GameCacheServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public GameCacheServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PES3-Cache-Test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Diy_staging_copies_to_cache_and_hits_on_second_call()
    {
        var gameRoot = Path.Combine(_tempRoot, "disc");
        Ps3DiscFixtureBuilder.WriteDiyDisc(gameRoot);
        var root = gameRoot + Path.DirectorySeparatorChar;

        var status = DiscDetector.GetVolumeStatus(root);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
        var game = status.Game!;

        var config = new Pes3Config
        {
            StorageMode = "PersistentLibrary",
            DeleteCacheAfterPlay = false,
            DumpCachePath = Path.Combine(_tempRoot, "cache"),
        };
        var paths = new Pes3Paths(config);
        var cache = new GameCacheService(config, paths);

        var drive = new OpticalDrive
        {
            Root = root,
            Id = "TESTVOL|diy",
            Letter = 'T',
        };

        var session1 = await cache.PrepareDiyPlayAsync(drive, game);
        Assert.False(session1.FromCache);
        Assert.Equal(Pes3LibraryTier.PersistentLibrary, session1.Tier);
        Assert.True(File.Exists(session1.EbootPath));

        var session2 = await cache.PrepareDiyPlayAsync(drive, game);
        Assert.True(session2.FromCache);
    }

    [Fact]
    public void DeleteCacheAfterPlay_disables_cache_lookup()
    {
        var config = new Pes3Config { DeleteCacheAfterPlay = true, DumpCachePath = Path.Combine(_tempRoot, "cache") };
        var paths = new Pes3Paths(config);
        var cache = new GameCacheService(config, paths);
        Assert.Null(cache.TryGetCached("vol", "BLUS99991", null));
    }

    [Fact]
    public async Task SmartHybrid_diy_uses_disc_assisted_overlay()
    {
        var gameRoot = Path.Combine(_tempRoot, "disc");
        Ps3DiscFixtureBuilder.WriteDiyDisc(gameRoot);
        var large = Path.Combine(gameRoot, "PS3_GAME", "USRDIR", "data", "big.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(large)!);
        File.WriteAllBytes(large, new byte[512 * 1024]);

        var root = gameRoot + Path.DirectorySeparatorChar;
        var status = DiscDetector.GetVolumeStatus(root);
        var game = status.Game!;

        var config = new Pes3Config
        {
            StorageMode = "SmartHybrid",
            DeleteCacheAfterPlay = false,
            DumpCachePath = Path.Combine(_tempRoot, "cache"),
            Rpcs3Path = Path.Combine(_tempRoot, "rpcs3.exe"),
        };
        File.WriteAllText(config.Rpcs3Path, "");
        var paths = new Pes3Paths(config);
        var cache = new GameCacheService(config, paths);
        var drive = new OpticalDrive { Root = root, Id = "VOL|smart", Letter = 'S' };

        var session = await cache.PrepareDiyPlayAsync(drive, game);
        Assert.Equal(Pes3LibraryTier.DiscAssistedOverlay, session.Tier);
        Assert.NotEqual(game.EbootPath, session.EbootPath);
        Assert.Single(session.CleanupDirs);
        Assert.True(File.Exists(session.EbootPath));
        Assert.True(Directory.Exists(session.CleanupDirs[0]));

        var overlayEboot = session.EbootPath;
        var linkedBig = Path.Combine(session.CleanupDirs[0], "PS3_GAME", "USRDIR", "data", "big.bin");
        if (File.Exists(linkedBig))
        {
            try
            {
                var target = File.ResolveLinkTarget(linkedBig, returnFinalTarget: true);
                Assert.Equal(Path.GetFullPath(large), Path.GetFullPath(target?.FullName ?? large));
            }
            catch
            {
                Assert.True(new FileInfo(linkedBig).Length >= 512 * 1024);
            }
        }
    }

    [Fact]
    public void PlaySessionRegistry_cleans_up_on_eject()
    {
        var sessionDir = Path.Combine(_tempRoot, "overlay-session");
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(Path.Combine(sessionDir, "marker.txt"), "x");

        var config = new Pes3Config { DumpCachePath = Path.Combine(_tempRoot, "cache") };
        var paths = new Pes3Paths(config);
        var registry = new PlaySessionRegistry(paths);

        registry.Register(new PlaySession
        {
            EbootPath = Path.Combine(sessionDir, "PS3_GAME", "USRDIR", "EBOOT.BIN"),
            CleanupDirs = new[] { sessionDir },
            VolumeId = "GONE|eject-test",
            DiscRoot = "Z:\\",
        });

        registry.UnregisterVolume("GONE|eject-test");
        Assert.False(Directory.Exists(sessionDir));
    }

    [Fact]
    public void PlaySessionRegistry_clear_volume_does_not_delete_twice()
    {
        var sessionDir = Path.Combine(_tempRoot, "clear-volume");
        Directory.CreateDirectory(sessionDir);
        var config = new Pes3Config { DumpCachePath = Path.Combine(_tempRoot, "cache") };
        var registry = new PlaySessionRegistry(new Pes3Paths(config));
        registry.Register(new PlaySession
        {
            EbootPath = Path.Combine(sessionDir, "eboot"),
            CleanupDirs = new[] { sessionDir },
            VolumeId = "VOL|clear",
        });
        SessionCleanup.DeleteTree(sessionDir);
        registry.ClearVolume("VOL|clear");
        Assert.False(Directory.Exists(sessionDir));
    }

    [Fact]
    public void FinalizeRetailDecrypt_smart_hybrid_promotes_to_library()
    {
        var config = new Pes3Config
        {
            StorageMode = "SmartHybrid",
            DumpCachePath = Path.Combine(_tempRoot, "cache"),
        };
        var paths = new Pes3Paths(config);
        var cache = new GameCacheService(config, paths);
        var sessionDir = Path.Combine(_tempRoot, "retail-session");
        Directory.CreateDirectory(Path.Combine(sessionDir, "PS3_GAME", "USRDIR"));
        var eboot = Path.Combine(sessionDir, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        File.WriteAllBytes(eboot, new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01 });

        var result = new DecryptResult
        {
            Success = true,
            Eboot = eboot,
            GameRoot = sessionDir,
            ProductCode = "BLUS99999",
            Title = "Test",
        };
        var cleanup = new List<string> { sessionDir };
        var session = cache.FinalizeRetailDecrypt(result, sessionDir, cleanup);

        Assert.Equal(Pes3LibraryTier.PersistentLibrary, session.Tier);
        Assert.Empty(session.CleanupDirs);
        Assert.True(Directory.Exists(paths.TitleInstallDir("BLUS99999")));
        Assert.NotNull(cache.TryGetCached("any-vol", null, "BLUS99999"));
    }

    [Fact]
    public void TryReadProductCodeFromVolume_reads_param_sfo()
    {
        var gameRoot = Path.Combine(_tempRoot, "retail-disc");
        Ps3DiscFixtureBuilder.WriteRetailDisc(gameRoot);
        var code = GameMetadata.TryReadProductCodeFromVolume(gameRoot + Path.DirectorySeparatorChar);
        Assert.False(string.IsNullOrWhiteSpace(code));
    }

    [Fact]
    public void Migrator_moves_legacy_cache_folder_to_library_titles()
    {
        var cacheRoot = Path.Combine(_tempRoot, "cache");
        var legacyTitle = Path.Combine(cacheRoot, "BLUS12345");
        Directory.CreateDirectory(legacyTitle);
        File.WriteAllText(Path.Combine(legacyTitle, "marker.txt"), "x");

        var config = new Pes3Config { DumpCachePath = cacheRoot };
        var paths = new Pes3Paths(config);
        Pes3LibraryMigrator.MigrateIfNeeded(paths);

        var target = paths.TitleInstallDir("BLUS12345");
        Assert.True(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(target, "marker.txt")));
        Assert.False(Directory.Exists(legacyTitle));
    }
}

public class Pes3StorageModeTests
{
    [Theory]
    [InlineData("SmartHybrid", Pes3StorageMode.SmartHybrid)]
    [InlineData("library", Pes3StorageMode.PersistentLibrary)]
    [InlineData("session", Pes3StorageMode.EphemeralSession)]
    [InlineData("disc-direct", Pes3StorageMode.DiscDirect)]
    public void TryParse_modes(string input, Pes3StorageMode expected)
    {
        Assert.True(Pes3StorageModeResolver.TryParse(input, out var mode));
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Legacy_delete_flag_maps_to_session()
    {
        var config = new Pes3Config { DeleteCacheAfterPlay = true };
        Assert.Equal(Pes3StorageMode.EphemeralSession, Pes3StorageModeResolver.Resolve(config));
    }

    [Fact]
    public void SmartHybrid_can_replay_from_library()
    {
        var config = new Pes3Config { StorageMode = "SmartHybrid" };
        Assert.True(Pes3StorageModeResolver.CanReplayFromLibrary(config));
        Assert.True(Pes3StorageModeResolver.PromotesRetailToLibrary(config));
    }

    [Fact]
    public void EphemeralSession_does_not_replay_from_library()
    {
        var config = new Pes3Config { StorageMode = "EphemeralSession" };
        Assert.False(Pes3StorageModeResolver.CanReplayFromLibrary(config));
    }

    [Fact]
    public void Apply_syncs_legacy_delete_flag()
    {
        var config = new Pes3Config();
        Pes3StorageModeResolver.Apply(config, Pes3StorageMode.EphemeralSession);
        Assert.True(config.DeleteCacheAfterPlay);
        Assert.Equal("EphemeralSession", config.StorageMode);
    }
}
