using System.Collections.Concurrent;
using System.Text.Json;

namespace PES3Disc.Core;

public sealed class PlaySessionRegistry
{
    private readonly ConcurrentDictionary<string, List<RegisteredSession>> _byVolume = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _persistPath;
    private readonly object _persistLock = new();

    public PlaySessionRegistry(Pes3Paths? paths = null)
    {
        if (paths?.Pes3Root is not null)
            _persistPath = Path.Combine(paths.Pes3Root, "state", "active-sessions.json");
    }

    public void Register(PlaySession session)
    {
        if (string.IsNullOrWhiteSpace(session.VolumeId))
            return;

        var entry = new RegisteredSession
        {
            VolumeId = session.VolumeId,
            DiscRoot = session.DiscRoot,
            CleanupDirs = session.CleanupDirs.ToList(),
            EbootPath = session.EbootPath,
            RegisteredUtc = DateTime.UtcNow,
        };

        var list = _byVolume.GetOrAdd(session.VolumeId, _ => new List<RegisteredSession>());
        lock (list)
        {
            list.Add(entry);
        }

        Persist();
        Pes3Log.Write($"Registered play session for volume {session.VolumeId} ({entry.CleanupDirs.Count} cleanup dir(s)).");
    }

    public void UnregisterVolume(string volumeId, bool deleteFiles = true)
    {
        if (_byVolume.TryRemove(volumeId, out var list))
        {
            if (deleteFiles)
            {
                lock (list)
                {
                    foreach (var entry in list)
                        SessionCleanup.DeleteTrees(entry.CleanupDirs);
                }
                Pes3Log.Write($"Eject cleanup for volume {volumeId}.");
            }
        }

        RemoveFromPersist(volumeId);
    }

    /// <summary>Drop tracking after RPCS3 exit cleanup (files already deleted).</summary>
    public void ClearVolume(string volumeId) => UnregisterVolume(volumeId, deleteFiles: false);

    public void ReconcileOnStartup()
    {
        if (_persistPath is null || !File.Exists(_persistPath))
            return;

        try
        {
            var present = new HashSet<string>(
                DiscDetector.GetOpticalDrives().Select(d => d.Id),
                StringComparer.OrdinalIgnoreCase);

            List<RegisteredSession> list;
            lock (_persistLock)
            {
                var json = File.ReadAllText(_persistPath);
                list = JsonSerializer.Deserialize<List<RegisteredSession>>(json, JsonOptions) ?? new();
            }

            foreach (var entry in list)
            {
                if (string.IsNullOrWhiteSpace(entry.VolumeId))
                    continue;

                if (!present.Contains(entry.VolumeId))
                {
                    SessionCleanup.DeleteTrees(entry.CleanupDirs);
                    RemoveFromPersist(entry.VolumeId);
                    continue;
                }

                var bucket = _byVolume.GetOrAdd(entry.VolumeId, _ => new List<RegisteredSession>());
                lock (bucket)
                {
                    if (!bucket.Any(e => string.Equals(e.EbootPath, entry.EbootPath, StringComparison.OrdinalIgnoreCase)))
                        bucket.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Session registry startup reconcile failed: {ex.Message}");
        }
    }

    public void CleanupEjectedVolumes()
    {
        var present = new HashSet<string>(
            DiscDetector.GetOpticalDrives().Select(d => d.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var volumeId in _byVolume.Keys.ToList())
        {
            if (!present.Contains(volumeId))
                UnregisterVolume(volumeId);
        }

        ReconcilePersistedSessions(present);
    }

    private void Persist()
    {
        if (_persistPath is null)
            return;

        try
        {
            var all = new List<RegisteredSession>();
            foreach (var kv in _byVolume)
            {
                lock (kv.Value)
                    all.AddRange(kv.Value);
            }

            var dir = Path.GetDirectoryName(_persistPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            lock (_persistLock)
            {
                File.WriteAllText(_persistPath, JsonSerializer.Serialize(all, JsonOptions));
            }
        }
        catch
        {
            // ignore
        }
    }

    private void RemoveFromPersist(string volumeId)
    {
        if (_persistPath is null || !File.Exists(_persistPath))
            return;

        try
        {
            lock (_persistLock)
            {
                var json = File.ReadAllText(_persistPath);
                var list = JsonSerializer.Deserialize<List<RegisteredSession>>(json, JsonOptions) ?? new();
                list.RemoveAll(s => string.Equals(s.VolumeId, volumeId, StringComparison.OrdinalIgnoreCase));
                File.WriteAllText(_persistPath, JsonSerializer.Serialize(list, JsonOptions));
            }
        }
        catch
        {
            // ignore
        }
    }

    private void ReconcilePersistedSessions(HashSet<string> presentVolumeIds)
    {
        if (_persistPath is null || !File.Exists(_persistPath))
            return;

        try
        {
            List<RegisteredSession> list;
            lock (_persistLock)
            {
                var json = File.ReadAllText(_persistPath);
                list = JsonSerializer.Deserialize<List<RegisteredSession>>(json, JsonOptions) ?? new();
            }

            foreach (var entry in list)
            {
                if (string.IsNullOrWhiteSpace(entry.VolumeId))
                    continue;
                if (!presentVolumeIds.Contains(entry.VolumeId))
                {
                    SessionCleanup.DeleteTrees(entry.CleanupDirs);
                    RemoveFromPersist(entry.VolumeId);
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class RegisteredSession
    {
        public string VolumeId { get; set; } = "";
        public string? DiscRoot { get; set; }
        public List<string> CleanupDirs { get; set; } = new();
        public string EbootPath { get; set; } = "";
        public DateTime RegisteredUtc { get; set; }
    }
}
