using System.Net.Http.Json;
using System.Text.Json;

namespace PES3Disc.BugReports;

public sealed class DevStatusClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public DevStatusClient(string? apiBaseUrl = null, HttpClient? httpClient = null)
    {
        _baseUrl = (apiBaseUrl ?? BugReportEndpoints.DefaultApiBaseUrl).TrimEnd('/');
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<DevStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{_baseUrl}/api/dev-status", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DevStatusResponse>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty dev status response.");
    }

    public async Task<DevStatusResponse> SetModeAsync(string devApiKey, string mode, CancellationToken ct = default)
    {
        var payload = new DevStatusUpdateRequest { Mode = mode.Trim().ToLowerInvariant() };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/api/dev-status")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Add("X-Dev-Key", devApiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Dev status update failed ({(int)response.StatusCode}): {detail}");
        }

        return await response.Content.ReadFromJsonAsync<DevStatusResponse>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty dev status response.");
    }

    public void Dispose() => _http.Dispose();
}
