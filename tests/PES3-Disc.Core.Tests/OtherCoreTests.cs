using PES3Disc.BugReports;
using PES3Disc.Core;
using PES3Disc.Core.Tests.Fixtures;

namespace PES3Disc.Core.Tests;

public class DevStatusLogicTests
{
    [Theory]
    [InlineData("auto", 13, DevStatusKind.Green)]  // 8 AM ET = 13 UTC (EST)
    [InlineData("auto", 3, DevStatusKind.Grey)]    // 10 PM ET previous = 3 UTC
    [InlineData("yellow", 13, DevStatusKind.Yellow)]
    [InlineData("green", 3, DevStatusKind.Green)]
    [InlineData("grey", 13, DevStatusKind.Grey)]
    public void ResolveEffective_respects_mode_and_schedule(string mode, int utcHour, DevStatusKind expected)
    {
        var utc = new DateTime(2026, 1, 15, utcHour, 30, 0, DateTimeKind.Utc);
        var kind = DevStatusLogic.ResolveEffective(mode, utc);
        Assert.Equal(expected, kind);
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
        var utc = new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc);
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
            "Game crashes on scan",
            "Insert disc and click scan, app closes",
            "Scan crash",
            "When I scan the disc the program crashes");
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
}
