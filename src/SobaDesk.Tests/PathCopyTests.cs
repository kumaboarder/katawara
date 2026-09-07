using NUnit.Framework;
using SobaDesk;

namespace SobaDesk.Tests;

public class PathCopyTests
{
    [Test]
    public void NameUsesLastSegmentNotCompactedLabel()
    {
        Assert.That(PathCopy.Name("src/SobaDesk", @"C:\proj"), Is.EqualTo("SobaDesk"));
        Assert.That(PathCopy.Name("docs/readme.md", @"C:\proj"), Is.EqualTo("readme.md"));
    }

    [Test]
    public void NameFallsBackToRootFolder()
    {
        Assert.That(PathCopy.Name("", @"C:\Users\kumab\hashauto"), Is.EqualTo("hashauto"));
    }

    [Test]
    public void WindowsRelativeAndFull()
    {
        Assert.That(PathCopy.Relative("docs/readme.md", @"C:\Users\kumab\hashauto"), Is.EqualTo(@"docs\readme.md"));
        Assert.That(
            PathCopy.Full(@"C:\Users\kumab\hashauto", "docs/readme.md"),
            Is.EqualTo(@"C:\Users\kumab\hashauto\docs\readme.md"));
    }

    [Test]
    public void UnixRelativeAndFull()
    {
        Assert.That(PathCopy.Relative("docs/readme.md", "/home/user/proj"), Is.EqualTo("docs/readme.md"));
        Assert.That(PathCopy.Full("/home/user/proj", "docs/readme.md"), Is.EqualTo("/home/user/proj/docs/readme.md"));
    }
}
