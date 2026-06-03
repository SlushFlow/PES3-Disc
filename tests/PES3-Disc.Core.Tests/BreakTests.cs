using PES3Disc.BugReports;
using PES3Disc.Core;
using PES3Disc.Core.Tests.Fixtures;

namespace PES3Disc.Core.Tests;

/// <summary>Adversarial / edge-case tests using DIY and retail fixture layouts.</summary>
public sealed class BreakTests : IDisposable
{
    private readonly string _tempRoot;

    public BreakTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PES3-Break-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { /* ignore */ }
    }

    [Fact]
    public void Swapped_eboot_headers_flip_classification()
    {
        var diyRoot = Path.Combine(_tempRoot, "mislabeled-diy");
        var retailRoot = Path.Combine(_tempRoot, "mislabeled-retail");
        Ps3DiscFixtureBuilder.WriteDiyDisc(diyRoot);
        Ps3DiscFixtureBuilder.WriteRetailDisc(retailRoot);

        var diyEboot = Path.Combine(diyRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        var retailEboot = Path.Combine(retailRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        var diyBytes = File.ReadAllBytes(diyEboot);
        var retailBytes = File.ReadAllBytes(retailEboot);
        File.WriteAllBytes(diyEboot, retailBytes);
        File.WriteAllBytes(retailEboot, diyBytes);

        var diyStatus = DiscDetector.GetVolumeStatus(diyRoot + Path.DirectorySeparatorChar);
        var retailStatus = DiscDetector.GetVolumeStatus(retailRoot + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.EncryptedRetail, diyStatus.Kind);
        Assert.Equal(DiscVolumeKind.Playable, retailStatus.Kind);
    }

    [Fact]
    public void Root_without_trailing_slash_matches_with_slash()
    {
        var root = Path.Combine(_tempRoot, "slash-test");
        Ps3DiscFixtureBuilder.WriteDiyDisc(root);
        var withSlash = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        var noSlash = DiscDetector.GetVolumeStatus(root);
        Assert.Equal(withSlash.Kind, noSlash.Kind);
        Assert.Equal(withSlash.Game?.TitleId, noSlash.Game?.TitleId);
    }

    [Fact]
    public void Path_with_spaces_in_folder_name()
    {
        var root = Path.Combine(_tempRoot, "My Game Disc");
        Ps3DiscFixtureBuilder.WriteDiyDisc(root);
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
    }

    [Fact]
    public void Empty_string_root_is_no_ps3_layout()
    {
        var ex = Record.Exception(() => DiscDetector.GetVolumeStatus(""));
        Assert.Null(ex);
        Assert.Equal(DiscVolumeKind.NoPs3Layout, DiscDetector.GetVolumeStatus("").Kind);
    }

    [Fact]
    public void Both_committed_fixtures_classify_oppositely()
    {
        var repo = Ps3DiscFixtureBuilder.FindRepoRoot();
        Assert.NotNull(repo);
        Ps3DiscFixtureBuilder.WriteStandardFixtures(repo);

        var diy = Path.Combine(repo, "test-fixtures", Ps3DiscFixtureBuilder.DiyFixtureName);
        var retail = Path.Combine(repo, "test-fixtures", Ps3DiscFixtureBuilder.RetailFixtureName);
        var diyStatus = DiscDetector.GetVolumeStatus(diy + Path.DirectorySeparatorChar);
        var retailStatus = DiscDetector.GetVolumeStatus(retail + Path.DirectorySeparatorChar);

        Assert.Equal(DiscVolumeKind.Playable, diyStatus.Kind);
        Assert.Equal(DiscVolumeKind.EncryptedRetail, retailStatus.Kind);
        Assert.NotEqual(diyStatus.Kind, retailStatus.Kind);
    }

    [Fact]
    public void DevStatusTracker_survives_unreachable_api()
    {
        using var tracker = new DevStatusTracker("http://127.0.0.1:1");
        tracker.Start(TimeSpan.FromSeconds(30));
        Thread.Sleep(50);
        tracker.Stop();
    }

    [Fact]
    public void BugReportLimits_rejects_null_title()
    {
        Assert.Throws<ArgumentException>(() => BugReportLimits.Validate(null!, "body"));
    }

    [Fact]
    public void Param_sfo_with_only_title_id_still_parses()
    {
        var root = Path.Combine(_tempRoot, "minimal-sfo");
        var ps3 = Path.Combine(root, "PS3_GAME", "USRDIR");
        Directory.CreateDirectory(ps3);
        Ps3DiscFixtureBuilder.WriteDecryptedEboot(Path.Combine(ps3, "EBOOT.BIN"));
        Ps3DiscFixtureBuilder.WriteParamSfo(Path.Combine(root, "PS3_GAME", "PARAM.SFO"), "BLUS00001", "Minimal");

        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
        Assert.Equal("BLUS00001", status.Game!.TitleId);
    }
}
