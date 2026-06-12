using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Ember;

public sealed record UsageItem(string Name, double Cost, int Calls);

public sealed record Snapshot(
    double Cost,
    int Calls,
    int Sessions,
    double CacheHitPercent,
    string Currency,
    IReadOnlyList<UsageItem> Models,
    IReadOnlyList<UsageItem> Projects);

public static class CodeburnClient
{
    public static Task<string> RunRawAsync(string arguments, CancellationToken ct = default) =>
        RunAsync("codeburn " + arguments, ct);

    public static async Task<Snapshot> FetchAsync(string period, CancellationToken ct = default)
    {
        var json = await RunAsync($"codeburn {period} --format json", ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var overview = root.GetProperty("overview");
        var currency = root.TryGetProperty("currency", out var c) ? c.GetString() ?? "USD" : "USD";

        return new Snapshot(
            Cost: overview.GetProperty("cost").GetDouble(),
            Calls: overview.GetProperty("calls").GetInt32(),
            Sessions: overview.TryGetProperty("sessions", out var s) ? s.GetInt32() : 0,
            CacheHitPercent: overview.TryGetProperty("cacheHitPercent", out var ch) ? ch.GetDouble() : 0,
            Currency: currency,
            Models: ReadItems(root, "models", useLeafOfPath: false),
            Projects: ReadItems(root, "projects", useLeafOfPath: true));
    }

    private static IReadOnlyList<UsageItem> ReadItems(JsonElement root, string key, bool useLeafOfPath)
    {
        var list = new List<UsageItem>();
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("name").GetString() ?? "?";
            if (useLeafOfPath && el.TryGetProperty("path", out var p) && p.GetString() is { Length: > 0 } path)
            {
                var leaf = Path.GetFileName(path.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(leaf)) name = leaf;
            }
            list.Add(new UsageItem(
                name,
                el.GetProperty("cost").GetDouble(),
                el.TryGetProperty("calls", out var ca) ? ca.GetInt32() : 0));
        }
        return list.OrderByDescending(i => i.Cost).ToList();
    }

    private static async Task<string> RunAsync(string command, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("cmd.exe", "/d /c " + command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));

        var stdout = proc.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = proc.StandardError.ReadToEndAsync(timeout.Token);
        await proc.WaitForExitAsync(timeout.Token);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"codeburn beendete sich mit Code {proc.ExitCode}: {(await stderr).Trim()}");

        return await stdout;
    }
}
