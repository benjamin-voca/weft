using System.Text.Json.Nodes;

namespace Weft.Cli;

/// <summary>Console formatting: ANSI colors (suppressed when piped) + tiny formatters.</summary>
public static class Out
{
    private static bool Color => !Console.IsOutputRedirected;

    public static string G(string s) => Color ? $"\x1b[32m{s}\x1b[0m" : s;   // green
    public static string R(string s) => Color ? $"\x1b[31m{s}\x1b[0m" : s;   // red
    public static string Y(string s) => Color ? $"\x1b[33m{s}\x1b[0m" : s;   // yellow
    public static string C(string s) => Color ? $"\x1b[36m{s}\x1b[0m" : s;   // cyan
    public static string D(string s) => Color ? $"\x1b[2m{s}\x1b[0m" : s;    // dim
    public static string B(string s) => Color ? $"\x1b[1m{s}\x1b[0m" : s;    // bold

    public static string S(JsonNode? n) => n?.GetValue<string>() ?? "";
    public static int I(JsonNode? n) => n?.GetValue<int>() ?? 0;
    public static double Dbl(JsonNode? n) => n?.GetValue<double>() ?? 0;

    public static string Duration(int ms)
    {
        if (ms <= 0) return "—";
        var secs = (int)Math.Round(ms / 1000.0);
        return secs < 60 ? $"{secs}s" : $"{secs / 60}m {secs % 60:D2}s";
    }

    public static string Rel(DateTime utc)
    {
        var secs = Math.Max(0, (int)(DateTime.UtcNow - utc).TotalSeconds);
        if (secs < 60) return "just now";
        if (secs < 3600) return $"{secs / 60}m ago";
        if (secs < 86400) return $"{secs / 3600}h ago";
        if (secs < 86400 * 14) return $"{secs / 86400}d ago";
        return utc.ToLocalTime().ToString("MMM d");
    }
}
