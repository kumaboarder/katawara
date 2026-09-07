using System.Text;
using System.Text.RegularExpressions;

namespace SobaDesk;

public static class HtmlPreview
{
    public const int MaxInlineBytes = 400_000;

    private static readonly Regex LinkTag = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImgTag = new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CssUrl = new(@"url\(\s*(['""]?)([^'""\)]+)\1\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ForIframe(string root, string relPath, bool strict)
    {
        var abs = WorkspaceTree.ResolveInside(root, relPath);
        var html = File.ReadAllText(abs);
        html = InlineStyles(html, root, relPath);
        html = InlineImages(html, root, relPath);
        html = HtmlGuards.WithIframeBridge(html);
        if (strict) html = HtmlGuards.WithCspMeta(html);
        return html;
    }

    public static string ResolveHref(string fromFileRel, string href)
    {
        var trimmed = (href ?? "").Trim();
        var cut = trimmed.Split('#', 2)[0].Split('?', 2)[0];
        if (cut.Length == 0) return "";
        var dummy = new Uri("http://workspace.local/" + fromFileRel.Replace('\\', '/'));
        var joined = new Uri(dummy, cut.Replace('\\', '/'));
        return Uri.UnescapeDataString(joined.AbsolutePath.TrimStart('/'));
    }

    public static bool IsExternal(string href)
    {
        var value = (href ?? "").Trim();
        if (value.Length == 0 || value.StartsWith('#') || value.StartsWith("?")) return true;
        return value.StartsWith("//", StringComparison.Ordinal)
               || value.Contains("://", StringComparison.Ordinal)
               || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    private static string InlineStyles(string html, string root, string fromRel)
    {
        return LinkTag.Replace(html, match =>
        {
            var tag = match.Value;
            if (!tag.Contains("stylesheet", StringComparison.OrdinalIgnoreCase)) return tag;
            var href = Attr(tag, "href");
            if (href is null || IsExternal(href)) return tag;
            var rel = ResolveHref(fromRel, href);
            if (!TryReadText(root, rel, out var css)) return tag;
            css = RewriteCssUrls(css, root, rel);
            return "<style data-soba-inline=\"1\">\n" + css + "\n</style>";
        });
    }

    private static string InlineImages(string html, string root, string fromRel)
    {
        return ImgTag.Replace(html, match =>
        {
            var tag = match.Value;
            var src = Attr(tag, "src");
            if (src is null || IsExternal(src)) return tag;
            var rel = ResolveHref(fromRel, src);
            if (!TryReadBytes(root, rel, out var bytes) || bytes.Length > MaxInlineBytes) return tag;
            var data = "data:" + MimeTypes.ForPath(rel).Split(';')[0] + ";base64," + Convert.ToBase64String(bytes);
            return SetAttr(tag, "src", data);
        });
    }

    private static string RewriteCssUrls(string css, string root, string cssRel)
    {
        return CssUrl.Replace(css, match =>
        {
            var href = match.Groups[2].Value.Trim();
            if (IsExternal(href) || href.StartsWith('#')) return match.Value;
            var rel = ResolveHref(cssRel, href);
            if (!TryReadBytes(root, rel, out var bytes) || bytes.Length > MaxInlineBytes) return match.Value;
            var data = "data:" + MimeTypes.ForPath(rel).Split(';')[0] + ";base64," + Convert.ToBase64String(bytes);
            return "url(\"" + data + "\")";
        });
    }

    private static bool TryReadText(string root, string rel, out string text)
    {
        text = "";
        if (!TryReadBytes(root, rel, out var bytes)) return false;
        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    private static bool TryReadBytes(string root, string rel, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(rel)) return false;
        try
        {
            var abs = WorkspaceTree.ResolveInside(root, rel);
            if (!File.Exists(abs) || WorkspaceTree.IsIgnored(root, rel, false)) return false;
            var info = new FileInfo(abs);
            if (info.Length > MaxInlineBytes) return false;
            bytes = File.ReadAllBytes(abs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? Attr(string tag, string name)
    {
        var quoted = Regex.Match(tag, name + @"\s*=\s*(['""])(.*?)\1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (quoted.Success) return quoted.Groups[2].Value;
        var bare = Regex.Match(tag, name + @"\s*=\s*([^\s>]+)", RegexOptions.IgnoreCase);
        return bare.Success ? bare.Groups[1].Value.Trim('"', '\'') : null;
    }

    private static string SetAttr(string tag, string name, string value)
    {
        var next = Regex.Replace(
            tag,
            name + @"\s*=\s*(['""])(.*?)\1",
            name + "=\"" + value.Replace("\"", "&quot;") + "\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!ReferenceEquals(next, tag) && next != tag) return next;
        if (tag.EndsWith("/>", StringComparison.Ordinal))
            return tag[..^2] + " " + name + "=\"" + value.Replace("\"", "&quot;") + "\" />";
        return tag[..^1] + " " + name + "=\"" + value.Replace("\"", "&quot;") + "\">";
    }
}
