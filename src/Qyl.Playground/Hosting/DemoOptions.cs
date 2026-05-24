namespace Qyl.Playground;

public sealed record DemoOptions(
    bool RunBoundedDemo,
    bool EnablePeriodicReporter,
    bool EnableLiveDashboard,
    TimeSpan Duration,
    int Parallelism,
    TimeSpan SnapshotInterval)
{
    public static DemoOptions FromArgs(string[] args)
    {
        var runDemo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var noDashboard = args.Contains("--no-dashboard", StringComparer.OrdinalIgnoreCase);
        var duration = ReadInt(args, "--duration", 8);
        var parallelism = ReadInt(args, "--parallelism", 4);
        var interval = ReadInt(args, "--interval", 1);

        // Auto-detect: Spectre live dashboard only when we own the terminal
        // AND demo mode is on. Piped / headless / CI runs fall through to the
        // periodic ILogger reporter so nothing fights with the consumer's stdout.
        var enableLiveDashboard = runDemo && !noDashboard && !Console.IsOutputRedirected;
        var enablePeriodicReporter =
            (runDemo && !enableLiveDashboard) ||
            args.Contains("--report", StringComparer.OrdinalIgnoreCase);

        return new DemoOptions(
            runDemo,
            enablePeriodicReporter,
            enableLiveDashboard,
            TimeSpan.FromSeconds(Math.Max(1, duration)),
            Math.Max(1, parallelism),
            TimeSpan.FromSeconds(Math.Max(1, interval)));
    }

    private static int ReadInt(string[] args, string name, int fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return fallback;
    }
}
