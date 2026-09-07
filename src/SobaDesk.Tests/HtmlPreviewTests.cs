using NUnit.Framework;
using SobaDesk;

namespace SobaDesk.Tests;

public class HtmlPreviewTests
{
    private string _root = "";

    [SetUp]
    public void SetUp() => _root = Directory.CreateTempSubdirectory("soba-html-").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch { /* ignore */ }
    }

    private void Write(string rel, string body)
    {
        var path = Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
    }

    [Test]
    public void ResolveHrefJoinsRelativeToHtml()
    {
        Assert.That(HtmlPreview.ResolveHref("preview/index.html", "./app.css"), Is.EqualTo("preview/app.css"));
        Assert.That(HtmlPreview.ResolveHref("preview/index.html", "../docs/a.md"), Is.EqualTo("docs/a.md"));
    }

    [Test]
    public void InlinesLocalStylesheetAndBridge()
    {
        Write("preview/app.css", "body { color: #f00; }");
        Write("preview/index.html", "<!DOCTYPE html><html><head><link rel=\"stylesheet\" href=\"./app.css\"></head><body><p>hi</p></body></html>");
        var html = HtmlPreview.ForIframe(_root, "preview/index.html", true);
        Assert.That(html.Contains("body { color: #f00; }"), Is.True);
        Assert.That(html.Contains("<link", StringComparison.OrdinalIgnoreCase), Is.False);
        Assert.That(html.Contains("data-soba-ctx"), Is.True);
        Assert.That(html.Contains("soba-nav"), Is.True);
        Assert.That(html.Contains("Content-Security-Policy"), Is.True);
    }
}
