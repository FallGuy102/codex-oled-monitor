using System.Text;
using System.Text.Json;

namespace CodexOledMonitor;

internal sealed class GameSenseClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private string? _address;

    private static string CorePropsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SteelSeries", "SteelSeries Engine 3", "coreProps.json");

    public async Task SendFrameAsync(int[] bytes, int value, CancellationToken cancellationToken)
    {
        await EnsureBoundAsync(cancellationToken).ConfigureAwait(false);
        var body = new Dictionary<string, object?>
        {
            ["game"] = "CODEX_OLED",
            ["event"] = "USAGE",
            ["data"] = new Dictionary<string, object?>
            {
                ["value"] = value,
                ["frame"] = new Dictionary<string, object?> { ["image-data-128x40"] = bytes }
            }
        };
        await PostAsync("game_event", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopGameAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_address is null) _address = ReadAddress();
            await PostAsync("stop_game", new { game = "CODEX_OLED" }, cancellationToken).ConfigureAwait(false);
        }
        catch { }
    }

    private async Task EnsureBoundAsync(CancellationToken cancellationToken)
    {
        var address = ReadAddress();
        if (_address == address) return;
        _address = address;

        await PostAsync("game_metadata", new
        {
            game = "CODEX_OLED",
            game_display_name = "Codex OLED Monitor",
            developer = "Local monitor",
            deinitialize_timer_length_ms = 60000
        }, cancellationToken).ConfigureAwait(false);

        var handler = new Dictionary<string, object?>
        {
            ["device-type"] = "screened-128x40",
            ["zone"] = "one",
            ["mode"] = "screen",
            ["datas"] = new object[]
            {
                new Dictionary<string, object?> { ["has-text"] = false, ["image-data"] = new int[640] }
            }
        };
        await PostAsync("bind_game_event", new Dictionary<string, object?>
        {
            ["game"] = "CODEX_OLED",
            ["event"] = "USAGE",
            ["min_value"] = 0,
            ["max_value"] = 100,
            ["icon_id"] = 0,
            ["value_optional"] = true,
            ["handlers"] = new object[] { handler }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string ReadAddress()
    {
        if (!File.Exists(CorePropsPath)) throw new IOException("SteelSeries GG 尚未运行。 ");
        using var document = JsonDocument.Parse(File.ReadAllText(CorePropsPath));
        return document.RootElement.GetProperty("address").GetString()
            ?? throw new IOException("SteelSeries GG 没有提供 GameSense 地址。 ");
    }

    private async Task PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"http://{_address}/{path}", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void ResetConnection() => _address = null;
    public void Dispose() => _http.Dispose();
}
