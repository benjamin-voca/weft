namespace Weft.Cli;

/// <summary>
/// Tiny git-style argument parser: verb, operands, --options (with or without
/// values), -s short flags. No external dependencies.
/// </summary>
public sealed class ParsedArgs
{
    public string? Verb { get; init; }
    public List<string> Operands { get; init; } = [];
    public Dictionary<string, string> Options { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> Flags { get; init; } = new(StringComparer.Ordinal);
    public bool Help { get; init; }
}

public static class ArgsParser
{
    /// <summary>Options that consume the next token as their value.</summary>
    private static readonly HashSet<string> ValuedShorts = ["-m", "-n", "-d", "-u", "-p"];
    private static readonly HashSet<string> ValuedLongs =
    [
        "--url", "--email", "--password", "--source", "--description", "--engine",
        "--samples", "--res", "--denoise", "--enable", "--disable", "--material",
        "--outputs", "--data", "--from", "--to", "--message",
    ];

    public static ParsedArgs Parse(string[] args)
    {
        string? verb = null;
        var operands = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var help = args.Contains("--help") || args.Contains("-h");

        string? pendingOption = null;
        foreach (var raw in args)
        {
            if (pendingOption is not null)
            {
                options[pendingOption] = raw;
                pendingOption = null;
                continue;
            }

            if (raw.StartsWith("--"))
            {
                var body = raw[2..];
                var eq = body.IndexOf('=');
                if (eq >= 0)
                {
                    options["--" + body[..eq]] = body[(eq + 1)..];
                }
                else if (body is "help")
                {
                    help = true;
                }
                else if (ValuedLongs.Contains("--" + body))
                {
                    pendingOption = "--" + body;
                }
                else
                {
                    flags.Add("--" + body);
                }
                continue;
            }

            if (raw.StartsWith('-') && raw.Length > 1)
            {
                if (ValuedShorts.Contains(raw))
                    pendingOption = raw;
                else
                    flags.Add(raw);
                continue;
            }

            if (verb is null) verb = raw;
            else operands.Add(raw);
        }

        if (pendingOption is not null)
            options[pendingOption] = "";

        return new ParsedArgs { Verb = verb, Operands = operands, Options = options, Flags = flags, Help = help };
    }

    /// <summary>First present option or flag value among the given keys, else null.</summary>
    public static string? Get(this ParsedArgs args, params string[] keys)
    {
        foreach (var k in keys)
            if (args.Options.TryGetValue(k, out var v)) return v;
        foreach (var k in keys)
            if (args.Flags.Contains(k)) return "";
        return null;
    }

    public static bool Flag(this ParsedArgs args, params string[] keys)
    {
        foreach (var k in keys)
            if (args.Flags.Contains(k) || args.Options.ContainsKey(k)) return true;
        return false;
    }
}
