using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PES3Disc.BugReports;

public sealed class BugReportClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public BugReportClient(string? apiBaseUrl = null, HttpClient? httpClient = null)
    {
        _baseUrl = (apiBaseUrl ?? BugReportEndpoints.DefaultApiBaseUrl).TrimEnd('/');
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<BugReportSubmitResult> SubmitAsync(BugReportSubmission submission, CancellationToken ct = default)
    {
        var (title, body) = BugReportLimits.Validate(submission.Title, submission.Body);
        var payload = new BugReportSubmission
        {
            Title = title,
            Body = body,
            Platform = submission.Platform,
            AppVersion = submission.AppVersion,
            OsDescription = TruncateOs(submission.OsDescription),
        };

        using var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/reports", payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Bug report failed ({(int)response.StatusCode}): {detail}");
        }

        var result = await response.Content.ReadFromJsonAsync<BugReportSubmitResult>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Empty response from bug report API.");
    }

    public async Task<IReadOnlyList<BugReportDto>> FetchReportsAsync(string devApiKey, DateTime? sinceUtc = null, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/api/reports";
        if (sinceUtc is not null)
            url += $"?since={Uri.EscapeDataString(sinceUtc.Value.ToUniversalTime().ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Dev-Key", devApiKey);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<BugReportDto>>(JsonOptions, ct);
        return list ?? [];
    }

    public async Task<IReadOnlyList<BugReportClusterSummary>> FetchSummariesAsync(string devApiKey, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/summaries");
        request.Headers.Add("X-Dev-Key", devApiKey);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<BugReportClusterSummary>>(JsonOptions, ct);
        return list ?? [];
    }

    public async Task ResolveReportAsync(
        string devApiKey,
        string reportId,
        BugReportResolutionStatus status,
        string? message = null,
        CancellationToken ct = default)
    {
        if (status == BugReportResolutionStatus.Open)
            throw new ArgumentException("Cannot resolve to open.");

        var payload = new BugReportResolveRequest
        {
            Status = status.ToApiValue(),
            Message = string.IsNullOrWhiteSpace(message) ? null : BugReportLimits.ValidateResolutionMessage(message),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/reports/{reportId}/resolve")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Add("X-Dev-Key", devApiKey);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Resolve failed ({(int)response.StatusCode}): {detail}");
        }
    }

    public async Task DeleteReportAsync(string devApiKey, string reportId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/api/reports/{reportId}");
        request.Headers.Add("X-Dev-Key", devApiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return;
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Delete failed ({(int)response.StatusCode}): {detail}");
        }
    }

    public async Task<BugReportResolutionDto?> FetchResolutionAsync(string reportId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{_baseUrl}/api/reports/{reportId}/resolution", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BugReportResolutionDto>(JsonOptions, ct);
    }

    private static string TruncateOs(string os)
    {
        os = (os ?? "").Trim();
        return os.Length <= 120 ? os : os[..120];
    }

    public void Dispose() => _http.Dispose();
}
