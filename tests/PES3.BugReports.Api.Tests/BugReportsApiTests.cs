using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PES3Disc.BugReports;

namespace PES3.BugReports.Api.Tests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "pes3-api-test-" + Guid.NewGuid().ToString("N") + ".db");

    public ApiWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("DATABASE_PATH", _dbPath);
        Environment.SetEnvironmentVariable("DEV_API_KEY", "test-dev-key");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
    }
}

public class BugReportsApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string DevKey = "test-dev-key";

    public BugReportsApiTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submit_report_without_title_fails()
    {
        var response = await _client.PostAsJsonAsync("/api/reports", new
        {
            title = "",
            body = "Something broke",
            platform = "windows",
            appVersion = "1.0.0",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_and_fetch_report_round_trip()
    {
        var submit = await _client.PostAsJsonAsync("/api/reports", new
        {
            title = "Scan crash",
            body = "App closes when scanning DIY disc",
            platform = "linux",
            appVersion = "1.0.0-test",
            osDescription = "CI",
        });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<SubmitReportResponse>();
        Assert.NotNull(created?.Id);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/reports");
        req.Headers.Add("X-Dev-Key", DevKey);
        var list = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Dev_status_get_and_put()
    {
        var get1 = await _client.GetFromJsonAsync<DevStatusResponse>("/api/dev-status");
        Assert.NotNull(get1);
        Assert.Equal("auto", get1!.Mode);

        var put = new HttpRequestMessage(HttpMethod.Put, "/api/dev-status")
        {
            Content = JsonContent.Create(new DevStatusUpdateRequest { Mode = "yellow" }),
        };
        put.Headers.Add("X-Dev-Key", DevKey);
        var putResp = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var get2 = await _client.GetFromJsonAsync<DevStatusResponse>("/api/dev-status");
        Assert.Equal("yellow", get2!.Mode);
        Assert.Equal("yellow", get2.Effective);
    }

    [Fact]
    public async Task Dev_status_put_without_key_unauthorized()
    {
        var put = new HttpRequestMessage(HttpMethod.Put, "/api/dev-status")
        {
            Content = JsonContent.Create(new DevStatusUpdateRequest { Mode = "green" }),
        };
        var resp = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_missing_report_returns_not_found()
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/reports/does-not-exist");
        req.Headers.Add("X-Dev-Key", DevKey);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Resolve_invalid_status_bad_request()
    {
        var submit = await _client.PostAsJsonAsync("/api/reports", new
        {
            title = "Resolve test",
            body = "For resolve validation",
            platform = "windows",
            appVersion = "1.0.0",
        });
        var created = await submit.Content.ReadFromJsonAsync<SubmitReportResponse>();
        Assert.NotNull(created?.Id);

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reports/{created!.Id}/resolve")
        {
            Content = JsonContent.Create(new { status = "invalid_status" }),
        };
        req.Headers.Add("X-Dev-Key", DevKey);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

internal sealed class SubmitReportResponse
{
    public string Id { get; set; } = "";
    public string? ClusterId { get; set; }
}
