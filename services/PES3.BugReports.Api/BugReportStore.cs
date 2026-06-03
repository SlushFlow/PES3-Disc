using Microsoft.Data.Sqlite;

namespace PES3.BugReports.Api;

public sealed class BugReportStore
{
    private readonly string _connectionString;

    public BugReportStore(string databasePath)
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
            CREATE TABLE IF NOT EXISTS Clusters (
                Id TEXT PRIMARY KEY,
                SummaryTitle TEXT NOT NULL,
                ReportCount INTEGER NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Reports (
                Id TEXT PRIMARY KEY,
                ClusterId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Body TEXT NOT NULL,
                Platform TEXT NOT NULL,
                AppVersion TEXT NOT NULL,
                OsDescription TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (ClusterId) REFERENCES Clusters(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_Reports_ClusterId ON Reports(ClusterId);
            CREATE INDEX IF NOT EXISTS IX_Reports_CreatedAtUtc ON Reports(CreatedAtUtc);
            """;
        cmd.ExecuteNonQuery();
        EnsureResolutionColumns(conn);
    }

    private static void EnsureResolutionColumns(SqliteConnection conn)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = conn.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(Reports)";
            using var reader = info.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        using var alter = conn.CreateCommand();
        if (!columns.Contains("Status"))
        {
            alter.CommandText = "ALTER TABLE Reports ADD COLUMN Status TEXT NOT NULL DEFAULT 'open'";
            alter.ExecuteNonQuery();
        }

        if (!columns.Contains("ResolutionMessage"))
        {
            alter.CommandText = "ALTER TABLE Reports ADD COLUMN ResolutionMessage TEXT";
            alter.ExecuteNonQuery();
        }

        if (!columns.Contains("ResolvedAtUtc"))
        {
            alter.CommandText = "ALTER TABLE Reports ADD COLUMN ResolvedAtUtc TEXT";
            alter.ExecuteNonQuery();
        }
    }

    public async Task<(ReportRecord Report, string ClusterId)> InsertReportAsync(
        string title,
        string body,
        string platform,
        string appVersion,
        string osDescription,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var reportId = Guid.NewGuid().ToString("N");
        var cluster = await FindOrCreateClusterAsync(title, body, now, ct);

        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO Reports (Id, ClusterId, Title, Body, Platform, AppVersion, OsDescription, CreatedAtUtc, Status)
                VALUES ($id, $clusterId, $title, $body, $platform, $appVersion, $os, $created, 'open')
                """;
            insert.Parameters.AddWithValue("$id", reportId);
            insert.Parameters.AddWithValue("$clusterId", cluster.Id);
            insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$body", body);
            insert.Parameters.AddWithValue("$platform", platform);
            insert.Parameters.AddWithValue("$appVersion", appVersion);
            insert.Parameters.AddWithValue("$os", osDescription);
            insert.Parameters.AddWithValue("$created", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(ct);
        }

        var summaryTitle = ReportClustering.PickSummaryTitle(cluster.SummaryTitle, title);
        using (var update = conn.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = """
                UPDATE Clusters SET SummaryTitle = $title, ReportCount = ReportCount + 1, UpdatedAtUtc = $updated
                WHERE Id = $id
                """;
            update.Parameters.AddWithValue("$title", summaryTitle);
            update.Parameters.AddWithValue("$updated", now.ToString("O"));
            update.Parameters.AddWithValue("$id", cluster.Id);
            await update.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
        return (new ReportRecord(reportId, cluster.Id, title, body, platform, appVersion, osDescription, now), cluster.Id);
    }

    private async Task<ClusterRecord> FindOrCreateClusterAsync(string title, string body, DateTime now, CancellationToken ct)
    {
        var centroids = await GetClusterCentroidsAsync(ct);
        ClusterCentroid? best = null;
        var bestScore = 0.0;
        foreach (var c in centroids)
        {
            var score = ReportClustering.Similarity(title, body, c.CentroidTitle, c.CentroidBody);
            if (score >= ReportClustering.ClusterSimilarityThreshold && score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best is not null)
            return new ClusterRecord(best.Id, best.SummaryTitle, 0, now);

        var clusterId = Guid.NewGuid().ToString("N");
        var summary = ReportClustering.TruncateTitle(title);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Clusters (Id, SummaryTitle, ReportCount, UpdatedAtUtc)
            VALUES ($id, $title, 0, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", clusterId);
        cmd.Parameters.AddWithValue("$title", summary);
        cmd.Parameters.AddWithValue("$updated", now.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
        return new ClusterRecord(clusterId, summary, 0, now);
    }

    private async Task<List<ClusterCentroid>> GetClusterCentroidsAsync(CancellationToken ct)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.Id, c.SummaryTitle, r.Title, r.Body
            FROM Clusters c
            INNER JOIN Reports r ON r.Id = (
                SELECT Id FROM Reports WHERE ClusterId = c.Id ORDER BY CreatedAtUtc DESC LIMIT 1
            )
            """;
        var list = new List<ClusterCentroid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ClusterCentroid(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }
        return list;
    }

    public async Task<IReadOnlyList<ReportDto>> ListReportsAsync(DateTime? sinceUtc, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sinceUtc is null
            ? "SELECT Id, ClusterId, Title, Body, Platform, AppVersion, OsDescription, CreatedAtUtc, Status, ResolutionMessage, ResolvedAtUtc FROM Reports ORDER BY CreatedAtUtc DESC"
            : "SELECT Id, ClusterId, Title, Body, Platform, AppVersion, OsDescription, CreatedAtUtc, Status, ResolutionMessage, ResolvedAtUtc FROM Reports WHERE CreatedAtUtc >= $since ORDER BY CreatedAtUtc DESC";
        if (sinceUtc is not null)
            cmd.Parameters.AddWithValue("$since", sinceUtc.Value.ToUniversalTime().ToString("O"));

        var list = new List<ReportDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadReportDto(reader));
        }
        return list;
    }

    public async Task<ReportResolutionDto?> GetResolutionAsync(string reportId, CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Title, Status, ResolutionMessage, ResolvedAtUtc
            FROM Reports WHERE Id = $id
            """;
        cmd.Parameters.AddWithValue("$id", reportId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ReportResolutionDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async Task<bool> ResolveReportAsync(string reportId, string status, string? message, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Reports
            SET Status = $status, ResolutionMessage = $message, ResolvedAtUtc = $resolved
            WHERE Id = $id AND (Status = 'open' OR Status IS NULL OR Status = '')
            """;
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$message", (object?)message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$resolved", now.ToString("O"));
        cmd.Parameters.AddWithValue("$id", reportId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<ClusterSummaryDto>> ListSummariesAsync(CancellationToken ct = default)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, SummaryTitle, ReportCount, UpdatedAtUtc FROM Clusters
            ORDER BY ReportCount DESC, UpdatedAtUtc DESC
            """;
        var clusters = new List<ClusterSummaryDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var clusterId = reader.GetString(0);
            var reports = await ListReportsForClusterAsync(conn, clusterId, ct);
            clusters.Add(new ClusterSummaryDto(
                clusterId,
                reader.GetString(1),
                reader.GetInt32(2),
                DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reports));
        }
        return clusters;
    }

    private static async Task<IReadOnlyList<ReportDto>> ListReportsForClusterAsync(SqliteConnection conn, string clusterId, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ClusterId, Title, Body, Platform, AppVersion, OsDescription, CreatedAtUtc, Status, ResolutionMessage, ResolvedAtUtc
            FROM Reports WHERE ClusterId = $clusterId ORDER BY CreatedAtUtc DESC
            """;
        cmd.Parameters.AddWithValue("$clusterId", clusterId);
        var list = new List<ReportDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(ReadReportDto(reader));
        return list;
    }

    private static ReportDto ReadReportDto(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new ReportDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : "open",
            reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : null,
            reader.FieldCount > 10 && !reader.IsDBNull(10)
                ? DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
