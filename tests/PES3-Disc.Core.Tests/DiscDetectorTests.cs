using PES3Disc.Core;
using PES3Disc.Core.Tests.Fixtures;

namespace PES3Disc.Core.Tests;

public sealed class DiscDetectorTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _repoRoot;

    public DiscDetectorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PES3-Disc-Test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
        _repoRoot = Ps3DiscFixtureBuilder.FindRepoRoot();
        if (_repoRoot is not null)
            Ps3DiscFixtureBuilder.WriteStandardFixtures(_repoRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { /* ignore */ }
    }

    [Fact]
    public void Committed_diy_fixture_is_playable()
    {
        Assert.NotNull(_repoRoot);
        var diy = Path.Combine(_repoRoot, "test-fixtures", Ps3DiscFixtureBuilder.DiyFixtureName);
        var status = DiscDetector.GetVolumeStatus(diy + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
        Assert.NotNull(status.Game);
        Assert.Equal(Ps3DiscFixtureBuilder.DiyTitleId, status.Game!.TitleId);
        Assert.Equal(Ps3DiscFixtureBuilder.DiyTitle, status.Game.Title);
    }

    [Fact]
    public void Committed_retail_fixture_is_encrypted_retail()
    {
        Assert.NotNull(_repoRoot);
        var retail = Path.Combine(_repoRoot, "test-fixtures", Ps3DiscFixtureBuilder.RetailFixtureName);
        var status = DiscDetector.GetVolumeStatus(retail + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.EncryptedRetail, status.Kind);
        Assert.Null(status.Game);
    }

    [Fact]
    public void Diy_standard_layout_detected()
    {
        var root = Path.Combine(_tempRoot, "diy-standard");
        Ps3DiscFixtureBuilder.WriteDiyDisc(root);
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
        Assert.Contains("EBOOT.BIN", status.Game!.EbootPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retail_encrypted_eboot_is_encrypted_retail()
    {
        var root = Path.Combine(_tempRoot, "retail-enc");
        Ps3DiscFixtureBuilder.WriteRetailDisc(root);
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.EncryptedRetail, status.Kind);
    }

    [Fact]
    public void Incomplete_ps3_game_without_eboot()
    {
        var root = Path.Combine(_tempRoot, "incomplete");
        Ps3DiscFixtureBuilder.WriteIncompleteDisc(root);
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.IncompleteBurn, status.Kind);
    }

    [Fact]
    public void Nested_game_folder_detected()
    {
        var root = Path.Combine(_tempRoot, "nested");
        Ps3DiscFixtureBuilder.WriteNestedDiyDisc(root);
        var game = DiscDetector.FindGameOnDrive(root + Path.DirectorySeparatorChar);
        Assert.NotNull(game);
        Assert.Contains("PS3_GAME", game!.EbootPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dev_bdvd_layout_detected()
    {
        var root = Path.Combine(_tempRoot, "dev-bdvd");
        Ps3DiscFixtureBuilder.WriteDevBdvdDisc(root);
        var game = DiscDetector.FindGameOnDrive(root + Path.DirectorySeparatorChar);
        Assert.NotNull(game);
        Assert.Contains("dev_bdvd", game!.EbootPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marker_only_volume_is_encrypted_retail()
    {
        var root = Path.Combine(_tempRoot, "marker-only");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "PS3_DISC.SFB"), "TEST");
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.EncryptedRetail, status.Kind);
    }

    [Fact]
    public void Empty_volume_is_no_ps3_layout()
    {
        var root = Path.Combine(_tempRoot, "empty");
        Directory.CreateDirectory(root);
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.NoPs3Layout, status.Kind);
    }

    [Fact]
    public void Bdmv_only_volume_is_no_ps3_layout()
    {
        var root = Path.Combine(_tempRoot, "bdmv");
        Directory.CreateDirectory(Path.Combine(root, "BDMV", "STREAM"));
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.NoPs3Layout, status.Kind);
    }

    [Fact]
    public void Corrupt_short_eboot_on_playable_tree_still_incomplete()
    {
        var root = Path.Combine(_tempRoot, "corrupt-eboot");
        var usr = Path.Combine(root, "PS3_GAME", "USRDIR");
        Directory.CreateDirectory(usr);
        File.WriteAllBytes(Path.Combine(usr, "EBOOT.BIN"), new byte[] { 0x7F, 0x45, 0x4C });
        var status = DiscDetector.GetVolumeStatus(root + Path.DirectorySeparatorChar);
        Assert.Equal(DiscVolumeKind.Playable, status.Kind);
    }

    [Fact]
    public void Nonexistent_root_does_not_throw()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist") + Path.DirectorySeparatorChar;
        var ex = Record.Exception(() => DiscDetector.GetVolumeStatus(missing));
        Assert.Null(ex);
        Assert.Equal(DiscVolumeKind.NoPs3Layout, DiscDetector.GetVolumeStatus(missing).Kind);
    }
}
