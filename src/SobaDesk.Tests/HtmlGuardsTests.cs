using NUnit.Framework;
using SobaDesk;

namespace SobaDesk.Tests;

public class HtmlGuardsTests
{
    [Test]
    public void CspDoesNotBlockFileParentFromFramingPreview()
    {
        Assert.That(HtmlGuards.PreviewCsp.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase), Is.False);
        var html = HtmlGuards.WithCspMeta("<html><head></head><body>ok</body></html>");
        Assert.That(html.Contains("frame-ancestors 'self'", StringComparison.Ordinal), Is.False);
    }

    [Test]
    public void ContextBridgeIsInjectedOnce()
    {
        var html = HtmlGuards.WithContextBridge("<html><body><p>hi</p></body></html>");
        Assert.That(html.Contains("data-soba-ctx"), Is.True);
        Assert.That(html.Contains("soba-ctx"), Is.True);
        var again = HtmlGuards.WithContextBridge(html);
        Assert.That(again.Split("data-soba-ctx", StringSplitOptions.None).Length, Is.EqualTo(2));
    }
}
