using System.Globalization;
using PES3.BugReports.Api;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "reports.db";
var devApiKey = Environment.GetEnvironmentVariable("DEV_API_KEY") ?? "dev-change-me";

var store = new BugReportStore(databasePath);
var rateLimiter = new SubmitRateLimiter(maxPerWindow: 10);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/reports", async (SubmitReportRequest req, HttpContext ctx, CancellationToken ct) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (!rateLimiter.Allow(ip))
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);

    try
    {
        var (title, body) = ReportClustering.Validate(req.Title, req.Body);
        var platform = (req.Platform ?? "").Trim();
        if (platform.Length == 0)
            return Results.BadRequest(new { error = "Platform is required." });

        var (report, clusterId) = await store.InsertReportAsync(
            title,
            body,
            platform,
            (req.AppVersion ?? "").Trim(),
            Truncate((req.OsDescription ?? "").Trim(), 120),
            ct);

        return Results.Created($"/api/reports/{report.Id}", new SubmitReportResponse(report.Id, clusterId));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/reports", async (HttpContext ctx, string? since, CancellationToken ct) =>
{
    if (!IsAuthorized(ctx, devApiKey))
        return Results.Unauthorized();

    DateTime? sinceUtc = null;
    if (!string.IsNullOrWhiteSpace(since))
    {
        if (!DateTime.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return Results.BadRequest(new { error = "Invalid since parameter." });
        sinceUtc = parsed.ToUniversalTime();
    }

    var reports = await store.ListReportsAsync(sinceUtc, ct);
    return Results.Ok(reports);
});

app.MapGet("/api/summaries", async (HttpContext ctx, CancellationToken ct) =>
{
    if (!IsAuthorized(ctx, devApiKey))
        return Results.Unauthorized();

    var summaries = await store.ListSummariesAsync(ct);
    return Results.Ok(summaries);
});

app.MapGet("/api/reports/{id}/resolution", async (string id, CancellationToken ct) =>
{
    var resolution = await store.GetResolutionAsync(id, ct);
    return resolution is null ? Results.NotFound() : Results.Ok(resolution);
});

app.MapPost("/api/reports/{id}/resolve", async (string id, ResolveReportRequest req, HttpContext ctx, CancellationToken ct) =>
{
    if (!IsAuthorized(ctx, devApiKey))
        return Results.Unauthorized();

    var status = (req.Status ?? "").Trim().ToLowerInvariant();
    if (status is not ("declined" or "to_be_fixed" or "fixed"))
        return Results.BadRequest(new { error = "Status must be declined, to_be_fixed, or fixed." });

    string? message = null;
    if (!string.IsNullOrWhiteSpace(req.Message))
    {
        message = req.Message.Trim();
        if (message.Length > 500)
            return Results.BadRequest(new { error = "Message must be at most 500 characters." });
    }

    var ok = await store.ResolveReportAsync(id, status, message, ct);
    return ok ? Results.Ok(new { id, status, message }) : Results.NotFound(new { error = "Report not found or already closed." });
});

app.Run();

static bool IsAuthorized(HttpContext ctx, string expectedKey)
{
    if (ctx.Request.Headers.TryGetValue("X-Dev-Key", out var key))
        return string.Equals(key.ToString(), expectedKey, StringComparison.Ordinal);
    return false;
}

static string Truncate(string value, int max)
    => value.Length <= max ? value : value[..max];
