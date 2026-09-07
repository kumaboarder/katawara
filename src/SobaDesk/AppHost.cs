using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Photino.NET;

namespace SobaDesk;

public sealed class AppHost
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public string WebRoot { get; }

    public string IndexPath => Path.Combine(WebRoot, "index.html");

    private FileSystemWatcher? _watcher;
    private PhotinoWindow? _window;
    private readonly object _watchLock = new();
    private CancellationTokenSource? _debounce;

    public AppHost()
    {
        WebRoot = Path.GetFullPath(Path.Combine(WorkspaceStore.ExeDir(), "wwwroot"));
    }

    public void Attach(PhotinoWindow window)
    {
        _window = window;
        RestartWatcher(WorkspaceStore.GetRoot());
    }

    public Stream ServeApp(object sender, string scheme, string url, out string contentType)
    {
        var rel = PathFromUrl(url);
        if (string.IsNullOrEmpty(rel) || rel.EndsWith('/')) rel += "index.html";
        var full = Path.GetFullPath(Path.Combine(WebRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = WebRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            contentType = "text/plain; charset=utf-8";
            return Bytes("not found");
        }
        contentType = MimeTypes.ForPath(full);
        return File.OpenRead(full);
    }

    public Stream ServePreview(object sender, string scheme, string url, out string contentType)
    {
        var rel = PathFromUrl(url);
        contentType = "text/plain; charset=utf-8";
        string root;
        try { root = WorkspaceStore.GetRoot(); }
        catch (Exception ex) { return Bytes(ex.Message); }
        if (string.IsNullOrEmpty(rel))
        {
            contentType = "text/html; charset=utf-8";
            return Bytes("<p>not found</p>");
        }
        string abs;
        try { abs = WorkspaceTree.ResolveInside(root, rel); }
        catch { return Bytes("not found"); }
        if (!File.Exists(abs) || WorkspaceTree.IsIgnored(root, rel, false))
            return Bytes("not found");

        var ext = Path.GetExtension(abs).ToLowerInvariant();
        if (ext is ".html" or ".htm")
        {
            string html;
            try { html = HtmlPreview.ForIframe(root, rel, LaunchOptions.Strict); }
            catch { html = File.ReadAllText(abs); }
            contentType = "text/html; charset=utf-8";
            return Bytes(html);
        }
        contentType = MimeTypes.ForPath(abs);
        return File.OpenRead(abs);
    }

    public void HandleMessage(PhotinoWindow window, string message)
    {
        JsonElement doc;
        try { doc = JsonSerializer.Deserialize<JsonElement>(message); }
        catch { return; }
        var id = doc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        var cmd = doc.TryGetProperty("cmd", out var cmdEl) ? cmdEl.GetString() ?? "" : "";
        try
        {
            object payload = cmd switch
            {
                "workspace" => WorkspacePayload(WorkspaceStore.GetState()),
                "setWorkspace" => SetWorkspace(doc),
                "browse" => Browse(window),
                "tree" => TreePayload(),
                "file" => FilePayload(doc),
                "openExternal" => OpenExternal(doc),
                "copyPath" => CopyPath(doc),
                _ => throw new InvalidOperationException("不明な要求です"),
            };
            window.SendWebMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["ok"] = true,
                ["data"] = payload,
            }, Json));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["ok"] = false,
                ["error"] = ex.Message,
            }, Json));
        }
    }

    private object SetWorkspace(JsonElement doc)
    {
        var root = doc.TryGetProperty("root", out var el) ? el.GetString() ?? "" : "";
        var state = WorkspaceStore.SetRoot(root);
        RestartWatcher(state.Root);
        return WorkspacePayload(state);
    }

    private object Browse(PhotinoWindow window)
    {
        var current = WorkspaceStore.GetRoot();
        var folders = window.ShowOpenFolder("Markdown と HTML だけを見るフォルダ", Directory.Exists(current) ? current : "", false);
        if (folders == null || folders.Length == 0)
            return new Dictionary<string, object?> { ["cancelled"] = true };
        var state = WorkspaceStore.SetRoot(folders[0]);
        RestartWatcher(state.Root);
        return WorkspacePayload(state);
    }

    private static object CopyPath(JsonElement doc)
    {
        var mode = doc.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() ?? "name" : "name";
        var rel = doc.TryGetProperty("path", out var pathEl) ? pathEl.GetString() ?? "" : "";
        var root = WorkspaceStore.GetRoot();
        var text = mode switch
        {
            "rel" => PathCopy.Relative(rel, root),
            "full" => PathCopy.Full(root, rel),
            _ => PathCopy.Name(rel, root),
        };
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("コピーする名前がありません");
        ClipboardText.TrySet(text);
        return new Dictionary<string, object?> { ["text"] = text };
    }

    private static object TreePayload()
    {
        var root = WorkspaceStore.GetRoot();
        var tree = WorkspaceTree.Build(root);
        var files = WorkspaceTree.FlattenFiles(tree);
        var md = files.Count(f => f.Kind == "md");
        var html = files.Count(f => f.Kind == "html");
        return new Dictionary<string, object?>
        {
            ["root"] = root,
            ["tree"] = tree,
            ["counts"] = new Dictionary<string, int> { ["md"] = md, ["html"] = html },
        };
    }

    private static object FilePayload(JsonElement doc)
    {
        var rel = doc.TryGetProperty("path", out var el) ? el.GetString() ?? "" : "";
        if (rel.Length == 0) throw new InvalidOperationException("path がありません");
        var root = WorkspaceStore.GetRoot();
        var abs = WorkspaceTree.ResolveInside(root, rel);
        if (WorkspaceTree.IsIgnored(root, rel, false)) throw new InvalidOperationException("このファイルは表示対象外です");
        var kind = WorkspaceTree.ClassifyFile(Path.GetFileName(rel));
        if (kind.Length == 0) throw new InvalidOperationException("Markdown か HTML だけ開けます");
        var content = File.ReadAllText(abs);
        string? previewHtml = null;
        if (kind == "html")
        {
            try { previewHtml = HtmlPreview.ForIframe(root, rel, LaunchOptions.Strict); }
            catch { previewHtml = content; }
        }
        var st = new FileInfo(abs);
        return new Dictionary<string, object?>
        {
            ["relPath"] = rel.Replace('\\', '/'),
            ["kind"] = kind,
            ["content"] = content,
            ["previewHtml"] = previewHtml,
            ["mtime"] = st.LastWriteTimeUtc.ToString("o"),
            ["size"] = st.Length,
        };
    }

    private static object OpenExternal(JsonElement doc)
    {
        var rel = doc.TryGetProperty("path", out var el) ? el.GetString() ?? "" : "";
        var root = WorkspaceStore.GetRoot();
        var abs = WorkspaceTree.ResolveInside(root, rel);
        if (WorkspaceTree.IsIgnored(root, rel, false) || !File.Exists(abs))
            throw new InvalidOperationException("ファイルを開けません");
        Process.Start(new ProcessStartInfo
        {
            FileName = abs,
            UseShellExecute = true,
        });
        return new Dictionary<string, bool> { ["opened"] = true };
    }

    private static Dictionary<string, object?> WorkspacePayload(StoreData state) => new()
    {
        ["root"] = state.Root,
        ["recent"] = state.Recent,
        ["platform"] = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "darwin" : "linux",
        ["canBrowse"] = true,
        ["strict"] = LaunchOptions.Strict,
    };

    private void RestartWatcher(string root)
    {
        lock (_watchLock)
        {
            _watcher?.Dispose();
            _watcher = null;
            if (!Directory.Exists(root)) return;
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            FileSystemEventHandler bang = (_, _) => DebounceWatch();
            watcher.Changed += bang;
            watcher.Created += bang;
            watcher.Deleted += bang;
            watcher.Renamed += (_, _) => DebounceWatch();
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
    }

    private void DebounceWatch()
    {
        lock (_watchLock)
        {
            _debounce?.Cancel();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    _window?.SendWebMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["push"] = "watch",
                        ["op"] = "change",
                    }, Json));
                }
                catch (TaskCanceledException) { }
            });
        }
    }

    private static string PathFromUrl(string url)
    {
        var colon = url.IndexOf("://", StringComparison.Ordinal);
        var rest = colon >= 0 ? url[(colon + 3)..] : url;
        rest = rest.TrimStart('/');
        if (rest.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase)) rest = rest["localhost/".Length..];
        else if (rest.Equals("localhost", StringComparison.OrdinalIgnoreCase)) rest = "";
        else if (rest.StartsWith("workspace/", StringComparison.OrdinalIgnoreCase)) rest = rest["workspace/".Length..];
        else if (rest.Equals("workspace", StringComparison.OrdinalIgnoreCase)) rest = "";
        else if (rest.StartsWith("ui/", StringComparison.OrdinalIgnoreCase)) rest = rest["ui/".Length..];
        else if (rest.Equals("ui", StringComparison.OrdinalIgnoreCase)) rest = "";
        var q = rest.IndexOf('?');
        if (q >= 0) rest = rest[..q];
        var hash = rest.IndexOf('#');
        if (hash >= 0) rest = rest[..hash];
        return string.Join('/', rest.Split('/').Select(Uri.UnescapeDataString).Where(s => s.Length > 0 && s != "." && s != ".."));
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
