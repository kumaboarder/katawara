namespace SobaDesk;

public static class PathCopy
{
    public static string Name(string relPath, string root)
    {
        var leaf = LastSegment(relPath);
        return leaf.Length > 0 ? leaf : LastSegment(root);
    }

    public static string Relative(string relPath, string root)
    {
        var sep = UsesWindowsSep(root) ? '\\' : '/';
        return Normalize(relPath).Replace('/', sep);
    }

    public static string Full(string root, string relPath)
    {
        var sep = UsesWindowsSep(root) ? '\\' : '/';
        var basePath = (root ?? "").TrimEnd('\\', '/');
        var rel = Relative(relPath, root ?? "");
        if (rel.Length == 0) return basePath;
        return basePath + sep + rel;
    }

    public static bool UsesWindowsSep(string root)
    {
        if (string.IsNullOrEmpty(root)) return false;
        if (root.Contains('\\')) return true;
        return root.Length >= 2 && char.IsAsciiLetter(root[0]) && root[1] == ':';
    }

    private static string Normalize(string path) =>
        (path ?? "").Replace('\\', '/').Trim('/');

    private static string LastSegment(string path)
    {
        var normalized = Normalize(path);
        if (normalized.Length == 0) return "";
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized[(slash + 1)..];
    }
}
