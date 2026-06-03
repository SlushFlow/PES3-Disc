namespace PES3Disc.BugReports;

/// <summary>Polls dev status and refreshes on auto schedule boundaries without app restart.</summary>
public sealed class DevStatusTracker : IDisposable
{
    private readonly string _apiUrl;
    private readonly string? _devApiKey;
    private DevStatusResponse? _cached;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly object _gate = new();

    public DevStatusTracker(string apiUrl, string? devApiKey = null)
    {
        _apiUrl = (apiUrl ?? BugReportEndpoints.DefaultApiBaseUrl).TrimEnd('/');
        _devApiKey = string.IsNullOrWhiteSpace(devApiKey) ? null : devApiKey.Trim();
    }

    public DevStatusResponse? Current
    {
        get
        {
            lock (_gate)
                return _cached;
        }
    }

    public event Action<DevStatusResponse?>? Changed;

    public void Start(TimeSpan pollInterval)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(pollInterval, _cts.Token);
    }

    public void Stop()
    {
        if (_cts is null)
            return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        _cts.Dispose();
        _cts = null;
    }

    public async Task RefreshFromServerAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new DevStatusClient(_apiUrl);
            var status = await client.GetStatusAsync(ct).ConfigureAwait(false);
            ApplyIfChanged(status);
        }
        catch
        {
            ApplyIfChanged(null);
        }
    }

    public async Task<DevStatusResponse> SetModeAsync(string mode, CancellationToken ct = default)
    {
        if (_devApiKey is null)
            throw new InvalidOperationException("Dev API key is required to set status.");

        using var client = new DevStatusClient(_apiUrl);
        var status = await client.SetModeAsync(_devApiKey, mode, ct).ConfigureAwait(false);
        ApplyIfChanged(status);
        return status;
    }

    public void ApplyLocalRefresh()
    {
        DevStatusResponse? next;
        lock (_gate)
        {
            if (_cached is null)
                return;
            next = DevStatusLogic.BuildDisplay(_cached.Mode, _cached.UpdatedAtUtc);
        }

        ApplyIfChanged(next);
    }

    private async Task RunLoopAsync(TimeSpan pollInterval, CancellationToken ct)
    {
        await RefreshFromServerAsync(ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            TimeSpan wait;
            lock (_gate)
            {
                wait = _cached is not null && DevStatusLogic.IsAutoMode(_cached.Mode)
                    ? MinDelay(pollInterval, DevStatusLogic.GetDelayUntilNextBoundary(DateTime.UtcNow))
                    : pollInterval;
            }

            try
            {
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var wasBoundary = wait < pollInterval;
            if (wasBoundary)
                ApplyLocalRefresh();

            await RefreshFromServerAsync(ct).ConfigureAwait(false);
        }
    }

    private static TimeSpan MinDelay(TimeSpan a, TimeSpan b) =>
        a < b ? a : b;

    private void ApplyIfChanged(DevStatusResponse? status)
    {
        DevStatusResponse? toRaise;
        lock (_gate)
        {
            if (status is not null && _cached is not null
                && string.Equals(_cached.Effective, status.Effective, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_cached.Mode, status.Mode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_cached.Label, status.Label, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _cached = status;
            toRaise = status;
        }

        Changed?.Invoke(toRaise);
    }

    public void Dispose()
    {
        Stop();
        try { _loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
    }
}
