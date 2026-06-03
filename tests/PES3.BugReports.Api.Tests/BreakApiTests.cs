using System.Net;
using System.Net.Http.Json;
using PES3Disc.BugReports;

namespace PES3.BugReports.Api.Tests;

public class BreakApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string DevKey = "test-dev-key";

    public BreakApiTests(ApiWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Dev_status_invalid_mode_returns_bad_request()
    {
        var put = new HttpRequestMessage(HttpMethod.Put, "/api/dev-status")
        {
            Content = JsonContent.Create(new DevStatusUpdateRequest { Mode = "purple" }),
        };
        put.Headers.Add("X-Dev-Key", DevKey);
        var resp = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Submit_report_missing_platform_fails()
    {
        var resp = await _client.PostAsJsonAsync("/api/reports", new
        {
            title = "No platform",
            body = "Missing platform field",
            platform = "",
            appVersion = "1.0.0",
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Z_rate_limiter_blocks_spam_submissions()
    {
        var gotCreated = false;
        var got429 = false;
        for (var i = 0; i < 15; i++)
        {
            var resp = await _client.PostAsJsonAsync("/api/reports", new
            {
                title = $"Spam {i}",
                body = "Rate limit break test",
                platform = "windows",
                appVersion = "1.0.0",
            });
            if (resp.StatusCode == HttpStatusCode.Created)
                gotCreated = true;
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                got429 = true;
                break;
            }
        }

        Assert.True(gotCreated);
        Assert.True(got429);
    }

    [Fact]
    public async Task Resolve_already_closed_report_returns_not_found()
    {
        var submit = await _client.PostAsJsonAsync("/api/reports", new
        {
            title = "Double close",
            body = "Try resolving twice",
            platform = "linux",
            appVersion = "1.0.0",
        });
        var created = await submit.Content.ReadFromJsonAsync<SubmitReportResponse>();
        Assert.NotNull(created?.Id);

        async Task<HttpResponseMessage> ResolveOnce()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reports/{created!.Id}/resolve")
            {
                Content = JsonContent.Create(new { status = "fixed", message = "done" }),
            };
            req.Headers.Add("X-Dev-Key", DevKey);
            return await _client.SendAsync(req);
        }

        Assert.Equal(HttpStatusCode.OK, (await ResolveOnce()).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ResolveOnce()).StatusCode);
    }

    [Fact]
    public async Task Report_id_with_path_traversal_does_not_escape()
    {
        var resp = await _client.GetAsync("/api/reports/../../etc/passwd/resolution");
        Assert.True(resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
    }
}
