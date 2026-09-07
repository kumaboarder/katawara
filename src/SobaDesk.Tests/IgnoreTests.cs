using NUnit.Framework;
using SobaDesk;

namespace SobaDesk.Tests;

public class IgnoreTests
{
    private string _root = "";

    [SetUp]
    public void SetUp() => _root = Directory.CreateTempSubdirectory("soba-").FullName;

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

    private HashSet<string> Rels() =>
        WorkspaceTree.FlattenFiles(WorkspaceTree.Build(_root)).Select(n => n.RelPath).ToHashSet();

    [Test]
    public void HidesNodePythonJavaFolders()
    {
        Write("docs/ok.md", "# ok");
        Write("node_modules/pkg/README.md", "# node");
        Write(".venv/lib/notes.md", "# py");
        Write("venv/notes.md", "# py2");
        Write("src/__pycache__/x.md", "# pyc");
        Write("target/generated.md", "# maven");
        Write(".gradle/ok.md", "# gradle");
        Write("app/build/reports.md", "# build");
        Write("frontend/dist/index.html", "<p>dist</p>");
        Write("coverage/lcov.md", "# cov");

        var got = Rels();
        Assert.That(got.Contains("docs/ok.md"), Is.True);
        foreach (var hidden in new[]
                 {
                     "node_modules/pkg/README.md",
                     ".venv/lib/notes.md",
                     "venv/notes.md",
                     "src/__pycache__/x.md",
                     "target/generated.md",
                     ".gradle/ok.md",
                     "app/build/reports.md",
                     "frontend/dist/index.html",
                     "coverage/lcov.md",
                 })
        {
            Assert.That(got.Contains(hidden), Is.False, hidden);
        }
    }

    [Test]
    public void SobaIgnoreNegationShowsDist()
    {
        Write(".sobaignore", "!dist/\n");
        Write("dist/preview/index.html", "<p>keep</p>");
        Write("node_modules/x/README.md", "# no");
        var got = Rels();
        Assert.That(got.Contains("dist/preview/index.html"), Is.True);
        Assert.That(got.Contains("node_modules/x/README.md"), Is.False);
    }

    [Test]
    public void NestedSobaIgnore()
    {
        Write("keep/a.md", "# a");
        Write("keep/skip/b.md", "# b");
        Write("keep/.sobaignore", "skip/\n");
        Write("other/skip/c.md", "# c");
        var got = Rels();
        Assert.That(got.Contains("keep/a.md"), Is.True);
        Assert.That(got.Contains("other/skip/c.md"), Is.True);
        Assert.That(got.Contains("keep/skip/b.md"), Is.False);
    }

    [Test]
    public void ParseLaunchArgsOpen()
    {
        Environment.SetEnvironmentVariable("SOBA_DESK_OPEN", "");
        LaunchOptions.Parse(["--open", @"C:\proj"]);
        Assert.That(LaunchOptions.Strict, Is.False);
        Assert.That(LaunchOptions.InitialRoot, Is.EqualTo(@"C:\proj"));
        LaunchOptions.Parse([]);
        Assert.That(LaunchOptions.Strict, Is.True);
    }
}
