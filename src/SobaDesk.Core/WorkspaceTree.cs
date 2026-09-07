using System.Text.Json.Serialization;

namespace SobaDesk;

public sealed class TreeNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("relPath")]
    public string RelPath { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TreeNode>? Children { get; set; }
}

public static class WorkspaceTree
{
    public static string ClassifyFile(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith(".md") || lower.EndsWith(".markdown")) return "md";
        if (lower.EndsWith(".html") || lower.EndsWith(".htm")) return "html";
        return "";
    }

    public static string ResolveInside(string root, string relPath)
    {
        var rootResolved = Path.GetFullPath(root);
        var combined = rootResolved;
        if (!string.IsNullOrWhiteSpace(relPath))
        {
            var parts = relPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            combined = Path.GetFullPath(Path.Combine(new[] { rootResolved }.Concat(parts).ToArray()));
        }
        var relative = Path.GetRelativePath(rootResolved, combined);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("ワークスペースの外は開けません");
        return combined;
    }

    public static List<TreeNode> Build(string root)
    {
        var abs = Path.GetFullPath(root);
        if (!Directory.Exists(abs)) throw new InvalidOperationException("フォルダではありません");
        var ign = IgnoreSet.LoadRoot(abs);
        var node = Walk(abs, "", ign);
        return node?.Children ?? [];
    }

    public static List<TreeNode> FlattenFiles(IEnumerable<TreeNode> nodes)
    {
        var files = new List<TreeNode>();
        void Visit(IEnumerable<TreeNode> list)
        {
            foreach (var node in list)
            {
                if (node.Kind == "dir" && node.Children != null) Visit(node.Children);
                else if (node.Kind != "dir") files.Add(node);
            }
        }
        Visit(nodes);
        return files;
    }

    public static bool IsIgnored(string root, string relPath, bool isDir)
        => IgnoreSet.LoadFor(root, relPath).Ignores(relPath, isDir);

    private static TreeNode? Walk(string abs, string relPath, IgnoreSet ign)
    {
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(abs); }
        catch { return null; }

        var local = ign;
        if (relPath.Length > 0)
        {
            var nested = Path.Combine(abs, IgnoreSet.FileName);
            if (File.Exists(nested)) local = ign.With(relPath, File.ReadAllText(nested));
        }

        var names = entries
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n) && n != IgnoreSet.FileName)
            .OrderBy(n => !Directory.Exists(Path.Combine(abs, n!)))
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var children = new List<TreeNode>();
        foreach (var name in names)
        {
            var childRel = relPath.Length == 0 ? name! : relPath + "/" + name;
            var childAbs = Path.Combine(abs, name!);
            var isDir = Directory.Exists(childAbs);
            if (local.Ignores(childRel, isDir)) continue;
            if (isDir)
            {
                var dir = Walk(childAbs, childRel, local);
                if (dir != null) children.Add(dir);
                continue;
            }
            var kind = ClassifyFile(name!);
            if (kind.Length == 0) continue;
            children.Add(new TreeNode { Name = name!, RelPath = childRel, Kind = kind });
        }

        if (children.Count == 0) return null;
        var display = relPath.Length == 0 ? Path.GetFileName(abs) : Path.GetFileName(relPath.Replace('/', Path.DirectorySeparatorChar));
        return new TreeNode { Name = display, RelPath = relPath, Kind = "dir", Children = children };
    }
}
