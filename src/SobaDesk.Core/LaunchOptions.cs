namespace SobaDesk;

public static class LaunchOptions
{
    public static bool Strict { get; private set; } = true;
    public static string? InitialRoot { get; private set; }

    public static void Parse(string[] args)
    {
        Strict = true;
        InitialRoot = null;
        foreach (var raw in args)
        {
            var arg = raw.Trim();
            switch (arg.ToLowerInvariant())
            {
                case "--open":
                case "-open":
                case "--no-guard":
                    Strict = false;
                    continue;
                case "--strict":
                case "-strict":
                    Strict = true;
                    continue;
            }
            if (arg.StartsWith('-')) continue;
            InitialRoot ??= arg;
        }
        var env = Environment.GetEnvironmentVariable("SOBA_DESK_OPEN");
        if (env == "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
            Strict = false;
        if (!string.IsNullOrWhiteSpace(InitialRoot))
            Environment.SetEnvironmentVariable("SOBA_DESK_ROOT", InitialRoot);
    }
}
