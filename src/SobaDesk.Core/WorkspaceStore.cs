using System.Text.Json;
using System.Text.Json.Serialization;

namespace SobaDesk;

public sealed class StoreData
{
    [JsonPropertyName("root")]
    public string Root { get; set; } = "";

    [JsonPropertyName("recent")]
    public List<string> Recent { get; set; } = [];
}

public static class WorkspaceStore
{
    public const int MaxRecent = 8;

    public static string StorePath() => Path.Combine(Path.GetTempPath(), "soba-desk-workspace.json");

    public static string ExeDir()
    {
        foreach (var dir in CandidateDirs())
        {
            if (File.Exists(Path.Combine(dir, "wwwroot", "index.html")) ||
                Directory.Exists(Path.Combine(dir, "demo-project")))
            {
                return dir;
            }
        }
        return CandidateDirs().First();
    }

    private static IEnumerable<string> CandidateDirs()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                seen.Add(Path.GetFullPath(path));
            }
            catch { /* ignore */ }
        }

        if (!string.IsNullOrEmpty(Environment.ProcessPath))
            Add(Path.GetDirectoryName(Environment.ProcessPath));
        try { Add(Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0]))); }
        catch { /* ignore */ }
        Add(AppContext.BaseDirectory);
        Add(Directory.GetCurrentDirectory());

        foreach (var dir in seen) yield return dir;
        if (seen.Count == 0) yield return Directory.GetCurrentDirectory();
    }

    public static string DemoRoot()
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(ExeDir(), "demo-project"),
                     Path.Combine(Directory.GetCurrentDirectory(), "demo-project"),
                 })
        {
            if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return Path.Combine(ExeDir(), "demo-project");
    }

    public static string Normalize(string root) => root.Trim().Trim('"', '\'');

    public static bool IsWindowsFolderPath(string root)
    {
        var value = Normalize(root);
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
            return true;
        return value.StartsWith(@"\\", StringComparison.Ordinal);
    }

    public static StoreData GetState()
    {
        if (TryRead(out var stored) && stored.Root.Length > 0)
        {
            return new StoreData
            {
                Root = Path.GetFullPath(stored.Root),
                Recent = stored.Recent.Select(SafeFull).ToList(),
            };
        }

        var fromEnv = Environment.GetEnvironmentVariable("SOBA_DESK_ROOT")?.Trim();
        if (!string.IsNullOrEmpty(fromEnv))
        {
            var resolved = Path.GetFullPath(Normalize(fromEnv));
            try { return SetRoot(resolved); }
            catch { return new StoreData { Root = resolved, Recent = [resolved] }; }
        }

        var fallback = Path.GetFullPath(DemoRoot());
        return new StoreData { Root = fallback, Recent = [] };
    }

    public static string GetRoot() => GetState().Root;

    public static StoreData SetRoot(string root)
    {
        var cleaned = Normalize(root);
        if (cleaned.Length == 0) throw new InvalidOperationException("フォルダを指定してください");
        if (IsWindowsFolderPath(cleaned) && !OperatingSystem.IsWindows())
            throw new InvalidOperationException("C:\\... は Windows 上のフォルダです。この PC で実行したアプリから開いてください。");
        var resolved = Path.GetFullPath(cleaned);
        if (!Directory.Exists(resolved)) throw new InvalidOperationException("フォルダが見つかりません: " + resolved);
        return Write(resolved);
    }

    private static bool TryRead(out StoreData data)
    {
        data = new StoreData();
        try
        {
            var raw = File.ReadAllText(StorePath());
            var parsed = JsonSerializer.Deserialize<StoreData>(raw);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Root)) return false;
            parsed.Recent ??= [parsed.Root];
            data = parsed;
            return true;
        }
        catch { return false; }
    }

    private static StoreData Write(string root)
    {
        TryRead(out var previous);
        var recent = new List<string> { root };
        foreach (var item in previous.Recent)
        {
            if (item != root) recent.Add(item);
        }
        if (recent.Count > MaxRecent) recent = recent.Take(MaxRecent).ToList();
        var next = new StoreData { Root = root, Recent = recent };
        File.WriteAllText(StorePath(), JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        return next;
    }

    private static string SafeFull(string item)
    {
        try { return Path.GetFullPath(item); }
        catch { return item; }
    }
}
