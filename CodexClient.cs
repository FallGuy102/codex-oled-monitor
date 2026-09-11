using System.Diagnostics;
using System.Text.Json;

namespace CodexOledMonitor;

internal sealed record LimitWindow(int UsedPercent, int? DurationMinutes, long? ResetsAt);
internal sealed record UsageSnapshot(LimitWindow? FiveHour, LimitWindow? Weekly, string Plan);

internal sealed class CodexClient : IAsyncDisposable
{
    private Process? _process;
    private StreamWriter? _input;
    private StreamReader? _output;
    private int _nextId = 1;

    public async Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var result = await SendRequestAsync("account/rateLimits/read", new { }, cancellationToken);
            return Parse(result);
        }
        catch
        {
            Stop();
            await EnsureStartedAsync(cancellationToken);
            var result = await SendRequestAsync("account/rateLimits/read", new { }, cancellationToken);
            return Parse(result);
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false }) return;

        var info = new ProcessStartInfo
        {
            FileName = FindCodex(),
            Arguments = "app-server --listen stdio://",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        _process = new Process { StartInfo = info, EnableRaisingEvents = true };
        _process.ErrorDataReceived += (_, _) => { };
        if (!_process.Start()) throw new InvalidOperationException("无法启动 Codex app-server。 ");
        _process.BeginErrorReadLine();
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _nextId = 1;

        await SendRequestAsync("initialize", new
        {
            clientInfo = new { name = "codex-oled-monitor", title = "Codex OLED Monitor", version = "3.0.0" }
        }, cancellationToken);
        await SendNotificationAsync("initialized", new { });
    }

    private async Task<JsonElement> SendRequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        if (_input is null || _output is null) throw new InvalidOperationException("Codex app-server 尚未启动。 ");
        var id = _nextId++;
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
        await _input.WriteLineAsync(payload);
        await _input.FlushAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            var line = await _output.ReadLineAsync(timeout.Token);
            if (line is null) throw new IOException("Codex app-server 意外退出。 ");
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch { continue; }
            using (document)
            {
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var messageId) || messageId.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out var error))
                    throw new InvalidOperationException(error.ToString());
                return root.GetProperty("result").Clone();
            }
        }
    }

    private async Task SendNotificationAsync(string method, object parameters)
    {
        if (_input is null) return;
        await _input.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));
        await _input.FlushAsync();
    }

    private static UsageSnapshot Parse(JsonElement result)
    {
        JsonElement bucket;
        if (result.TryGetProperty("rateLimitsByLimitId", out var groups) &&
            groups.ValueKind == JsonValueKind.Object && groups.TryGetProperty("codex", out var codex))
            bucket = codex;
        else
            bucket = result.GetProperty("rateLimits");

        var windows = new List<LimitWindow>();
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object) continue;
            var used = node.GetProperty("usedPercent").GetInt32();
            int? duration = node.TryGetProperty("windowDurationMins", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetInt32() : null;
            long? resets = node.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetInt64() : null;
            windows.Add(new LimitWindow(used, duration, resets));
        }

        var fiveHour = windows.FirstOrDefault(w => w.DurationMinutes == 300);
        var weekly = windows.FirstOrDefault(w => w.DurationMinutes == 10080);
        if (fiveHour is null && weekly is null && windows.Count > 0) fiveHour = windows[0];
        if (weekly is null && windows.Count > 1) weekly = windows[1];
        var plan = bucket.TryGetProperty("planType", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? "" : "";
        return new UsageSnapshot(fiveHour, weekly, plan.ToUpperInvariant());
    }

    private static string FindCodex()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new[]
        {
            Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
            Path.Combine(roaming, "npm", "codex.cmd")
        };
        var direct = candidates.FirstOrDefault(File.Exists);
        if (direct is not null) return direct;

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = Path.Combine(directory.Trim(), "codex.exe");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("找不到 Codex Desktop/CLI。 ");
    }

    private void Stop()
    {
        try
        {
            if (_process is { HasExited: false }) _process.Kill(true);
        }
        catch { }
        _input?.Dispose();
        _output?.Dispose();
        _process?.Dispose();
        _input = null;
        _output = null;
        _process = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
