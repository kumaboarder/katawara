namespace SobaDesk;

public static class MimeTypes
{
    public static string ForPath(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".md" => "text/markdown; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".txt" => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    };
}

public static class HtmlGuards
{
    public const string PreviewCsp =
        "default-src 'none'; script-src 'unsafe-inline' 'unsafe-eval'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; form-action 'none'; object-src 'none'; frame-src 'none'; base-uri 'none'";

    public static string WithBase(string html, string relPath, string hrefPrefix)
    {
        if (html.Contains("<base ", StringComparison.OrdinalIgnoreCase)) return html;
        var dir = Path.GetDirectoryName(relPath.Replace('\\', '/'))?.Replace('\\', '/') ?? "";
        var href = string.IsNullOrEmpty(dir) || dir == "." ? hrefPrefix : hrefPrefix.TrimEnd('/') + "/" + dir.Trim('/') + "/";
        return InsertHead(html, $"<base href=\"{href}\">");
    }

    public static string WithCspMeta(string html)
    {
        if (html.Contains("content-security-policy", StringComparison.OrdinalIgnoreCase)) return html;
        return InsertHead(html, $"<meta http-equiv=\"Content-Security-Policy\" content=\"{PreviewCsp}\">");
    }

    public static string WithContextBridge(string html) => WithIframeBridge(html);

    public static string WithIframeBridge(string html)
    {
        if (html.Contains("data-soba-ctx", StringComparison.Ordinal)) return html;
        const string tag =
            "<script data-soba-ctx=\"1\">(function(){" +
            "function sel(){try{return String(window.getSelection())}catch(e){return \"\"}}" +
            "function hrefOf(t){var a=t&&t.closest?t.closest(\"a\"):null;return a?(a.getAttribute(\"href\")||a.href||\"\"):\"\"}" +
            "document.addEventListener(\"contextmenu\",function(e){e.preventDefault();e.stopPropagation();try{parent.postMessage({kind:\"soba-ctx\",href:hrefOf(e.target),text:sel(),x:e.clientX,y:e.clientY},\"*\")}catch(x){}},true);" +
            "document.addEventListener(\"click\",function(e){var a=e.target&&e.target.closest?e.target.closest(\"a[href]\"):null;if(!a)return;var href=a.getAttribute(\"href\")||\"\";if(!href||href.charAt(0)===\"#\"||/^(https?:|mailto:|javascript:)/i.test(href))return;e.preventDefault();try{parent.postMessage({kind:\"soba-nav\",href:href},\"*\")}catch(x){}},true);" +
            "})();</script>";
        var lower = html.ToLowerInvariant();
        var idx = lower.LastIndexOf("</body>", StringComparison.Ordinal);
        if (idx >= 0) return html[..idx] + tag + html[idx..];
        return html + tag;
    }

    private static string InsertHead(string html, string tag)
    {
        var lower = html.ToLowerInvariant();
        var idx = lower.IndexOf("<head", StringComparison.Ordinal);
        if (idx < 0) return tag + html;
        var end = html.IndexOf('>', idx);
        if (end < 0) return tag + html;
        return html[..(end + 1)] + tag + html[(end + 1)..];
    }
}
