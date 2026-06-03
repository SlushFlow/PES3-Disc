using Microsoft.Data.Sqlite;
using PES3Disc.BugReports;

namespace PES3.BugReports.Api;

public sealed class DevStatusStore
{
    private readonly string _connectionString;

    public DevStatusStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ConnectionString;
        Initialize();
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS DevStatus (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                ManualMode TEXT NOT NULL DEFAULT 'auto',
                UpdatedAtUtc TEXT NOT NULL
            );
            INSERT OR IGNORE INTO DevStatus (Id, ManualMode, UpdatedAtUtc)
            VALUES (1, 'auto', @now);
            """;
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public async Task<(string ManualMode, DateTime UpdatedAtUtc)> GetAsync(CancellationToken ct)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ManualMode, UpdatedAtUtc FROM DevStatus WHERE Id = 1";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return ("auto", DateTime.UtcNow);

        var mode = reader.GetString(0);
        var updated = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
        return (mode, updated.ToUniversalTime());
    }

    public async Task<(string ManualMode, DateTime UpdatedAtUtc)> SetManualModeAsync(string mode, CancellationToken ct)
    {
        mode = NormalizeMode(mode);
        var now = DateTime.UtcNow;
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO DevStatus (Id, ManualMode, UpdatedAtUtc) VALUES (1, @mode, @now)
            ON CONFLICT(Id) DO UPDATE SET ManualMode = @mode, UpdatedAtUtc = @now;
            """;
        cmd.Parameters.AddWithValue("@mode", mode);
        cmd.Parameters.AddWithValue("@now", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return (mode, now);
    }

    public DevStatusResponse BuildResponse(string manualMode, DateTime updatedAtUtc)
    {
        var effective = DevStatusLogic.ResolveEffective(manualMode, DateTime.UtcNow);
        return new DevStatusResponse
        {
            Effective = effective.ToApiValue(),
            Mode = manualMode,
            Label = DevStatusLogic.GetLabel(effective),
            IsAutoSchedule = DevStatusLogic.IsAutoMode(manualMode),
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static string NormalizeMode(string mode)
    {
        mode = (mode ?? "auto").Trim().ToLowerInvariant();
        return mode switch
        {
            "auto" => "auto",
            "green" or "yellow" or "grey" or "gray" => mode == "gray" ? "grey" : mode,
            _ => throw new ArgumentException("Mode must be auto, green, yellow, or grey."),
        };
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
