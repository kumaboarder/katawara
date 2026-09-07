using System.Text.RegularExpressions;

namespace SobaDesk;

public sealed class IgnoreSet
{
    public const string FileName = ".sobaignore";

    public const string Defaults = """
# VCS / editors
.git/
.svn/
.hg/
.idea/
.vscode/
.obsidian/
.settings/

# hidden files and folders
.*

# Node.js
node_modules/
bower_components/
.npm/
.yarn/
.pnpm-store/
.next/
.nuxt/
.output/
.svelte-kit/
.angular/
.turbo/
.parcel-cache/
.vite/
.cache/
dist/
build/
out/
coverage/

# Python
__pycache__/
.venv/
venv/
env/
virtualenv/
.tox/
.nox/
.pytest_cache/
.mypy_cache/
.ruff_cache/
.ipynb_checkpoints/
.eggs/
*.egg-info/
site-packages/
htmlcov/

# Java
target/
.gradle/
.mvn/
bin/
classes/
*.class
""";

    private readonly List<IgnoreRule> _rules = [];

    public static IgnoreSet CreateDefault() => new IgnoreSet().With("", Defaults);

    public IgnoreSet With(string basePath, string text)
    {
        var next = new IgnoreSet();
        next._rules.AddRange(_rules);
        next._rules.AddRange(ParseRules(basePath, text));
        return next;
    }

    public bool Ignores(string rel, bool isDir)
    {
        rel = rel.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(rel) || rel == ".") return false;
        var parts = rel.Split('/');
        var acc = "";
        for (var i = 0; i < parts.Length; i++)
        {
            acc = acc.Length == 0 ? parts[i] : acc + "/" + parts[i];
            var dir = i < parts.Length - 1 || isDir;
            if (Match(acc, dir)) return true;
        }
        return false;
    }

    private bool Match(string rel, bool isDir)
    {
        var ignored = false;
        foreach (var rule in _rules)
        {
            if (rule.MatchOne(rel, isDir)) ignored = !rule.Negate;
        }
        return ignored;
    }

    public static IgnoreSet LoadRoot(string root)
    {
        var ign = CreateDefault();
        var path = Path.Combine(root, FileName);
        return File.Exists(path) ? ign.With("", File.ReadAllText(path)) : ign;
    }

    public static IgnoreSet LoadFor(string root, string relPath)
    {
        var ign = LoadRoot(root);
        relPath = relPath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(relPath)) return ign;
        var parts = relPath.Split('/');
        var acc = "";
        for (var i = 0; i < parts.Length - 1; i++)
        {
            acc = acc.Length == 0 ? parts[i] : acc + "/" + parts[i];
            var file = Path.Combine(root, acc.Replace('/', Path.DirectorySeparatorChar), FileName);
            if (File.Exists(file)) ign = ign.With(acc, File.ReadAllText(file));
        }
        return ign;
    }

    private static List<IgnoreRule> ParseRules(string basePath, string text)
    {
        basePath = basePath.Replace('\\', '/').Trim('/');
        var rules = new List<IgnoreRule>();
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var negate = false;
            if (line.StartsWith('!'))
            {
                negate = true;
                line = line[1..].Trim();
                if (line.Length == 0) continue;
            }
            var dirOnly = false;
            if (line.EndsWith('/'))
            {
                dirOnly = true;
                line = line.TrimEnd('/');
            }
            var anywhere = false;
            if (line.StartsWith('/')) line = line.TrimStart('/');
            else if (!line.Contains('/')) anywhere = true;
            line = line.Trim('/');
            if (line.Length == 0) continue;
            rules.Add(new IgnoreRule(negate, dirOnly, anywhere, basePath, GlobRegex(line)));
        }
        return rules;
    }

    private static Regex GlobRegex(string pattern)
    {
        var b = new System.Text.StringBuilder("^");
        var i = 0;
        while (i < pattern.Length)
        {
            if (i + 1 < pattern.Length && pattern[i] == '*' && pattern[i + 1] == '*')
            {
                b.Append(".*");
                i += 2;
                if (i < pattern.Length && pattern[i] == '/') i++;
                continue;
            }
            switch (pattern[i])
            {
                case '*': b.Append("[^/]*"); break;
                case '?': b.Append("[^/]"); break;
                case '.':
                case '+':
                case '(':
                case ')':
                case '|':
                case '^':
                case '$':
                case '{':
                case '}':
                case '[':
                case ']':
                case '\\':
                    b.Append('\\').Append(pattern[i]);
                    break;
                default:
                    b.Append(pattern[i]);
                    break;
            }
            i++;
        }
        b.Append('$');
        return new Regex(b.ToString(), RegexOptions.CultureInvariant);
    }

    private sealed record IgnoreRule(bool Negate, bool DirOnly, bool Anywhere, string Base, Regex Re)
    {
        public bool MatchOne(string rel, bool isDir)
        {
            if (DirOnly && !isDir) return false;
            rel = rel.Trim('/');
            var target = rel;
            if (Base.Length > 0)
            {
                if (rel != Base && !rel.StartsWith(Base + "/", StringComparison.Ordinal)) return false;
                if (rel == Base) return false;
                target = rel[(Base.Length + 1)..];
            }
            if (Anywhere)
            {
                if (Re.IsMatch(Path.GetFileName(target.Replace('/', Path.DirectorySeparatorChar)))) return true;
                var parts = target.Split('/');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (Re.IsMatch(string.Join('/', parts[i..]))) return true;
                }
                return false;
            }
            return Re.IsMatch(target);
        }
    }
}
