using System.Text;
using System.Text.Json.Nodes;

namespace Weft.Cli;

public static class Manual
{
    public const string Overview =
        """
        bldr — version control for your Blender projects

        usage: bldr <command> [arguments] [options]

        productions
          projects                      list productions on the server
          init <name>                   create a production (--description "…")
          use <name>                    set the active production (persists locally)
          status                        active production + asset overview

        assets (the version control core)
          add <name|.|path>             put a new asset under version control (v1)
          edit <asset>                  change the draft (--source --description
                                        --engine --samples --res WxH --denoise on|off
                                        --enable out --disable out --material n=s
                                        --lock --unlock)
          commit [-a] [-m "msg"] [as.]  snapshot the draft (auto-message if -m omitted)
          discard [asset]               throw the draft away, back to HEAD
          log <asset> [-n 20]           commit history
          diff [asset] [--from v --to v]  draft vs HEAD by default
          rollback <asset> <vN>         restore an old snapshot as a new commit
          rm <asset> --yes              remove an asset entirely

        renders
          render <asset> <output>       queue a render (--data '{"frame":42}')
          renders [asset] [-n 20]       recent render jobs
          replay <renderId>             re-run a render on the current snapshot

        session
          login [--url u] [--email e] [--password p]
          logout                        forget the session
          whoami                        show the signed-in user
          version                       print the version

        options
          -h, --help                    show help for any command

        typical flow
          bldr login
          bldr use "Coffee Run"
          bldr add Bella --source //characters/bella/bella_rig.blend
          bldr edit Bella --samples 256 --enable turntable
          bldr status
          bldr commit -m "firstrig"
          bldr log Bella
          bldr render Bella final
        """;

    public static readonly Dictionary<string, string> PerCommand = new(StringComparer.OrdinalIgnoreCase)
    {
        ["login"] = "usage: bldr login [--url url] [--email email] [--password password]\n\nauthenticates against a weft server and stores the session in ~/.bldr/config.json",
        ["logout"] = "usage: bldr logout\n\nends the server session and forgets it locally",
        ["whoami"] = "usage: bldr whoami\n\nshows the signed-in user",
        ["projects"] = "usage: bldr projects\n\nlists productions with their asset counts",
        ["init"] = "usage: bldr init <name> [--description \"...\"]\n\ncreates a production and makes it active",
        ["use"] = "usage: bldr use <name>\n\nsets the active production for this machine (no name: shows current)",
        ["status"] = "usage: bldr status\n\nshows the active production and every asset: version, hash, draft state, renders",
        ["add"] = """
            usage: bldr add <name|.|path> [--source //pool/file.blend] [--description "…"]
                   [--engine CYCLES|EEVEE] [--samples n] [--res WxH] [--denoise on|off]
                   [--outputs "final,viewport"]

            puts a new asset under version control and commits v1 ("Initial commit").
            'bldr add .' uses the current directory name and //dir/dir.blend as source.
            """,
        ["edit"] = """
            usage: bldr edit <asset> [changes…]

            changes (combine in one run):
              --source path            --description "…"
              --engine CYCLES|EEVEE    --samples n
              --res WxH                --denoise on|off
              --enable output          --disable output     (comma separated ok)
              --material "name=shader" --lock / --unlock

            the draft auto-saves server-side; 'bldr diff' shows what changed.
            """,
        ["commit"] = """
            usage: bldr commit [asset] [-m "message"] [-a]

            snapshots the draft as a new immutable commit. without -m the message is
            generated from the diff. with no asset named: commits the single dirty
            asset, or all of them with -a.
            """,
        ["discard"] = "usage: bldr discard [asset]\n\nthrows the draft away and restores HEAD into the working copy",
        ["log"] = "usage: bldr log <asset> [-n 20]\n\ncommit history, newest first",
        ["diff"] = """
            usage: bldr diff [asset] [--from vN] [--to vM]

            draft vs HEAD by default. --from/--to compare any two committed versions.
            """,
        ["rollback"] = """
            usage: bldr rollback <asset> <vN> [-m "message"]

            restores an old snapshot as a NEW commit — history is never rewritten.
            """,
        ["rm"] = "usage: bldr rm <asset> --yes\n\nremoves an asset and its entire history (irreversible)",
        ["render"] = """
            usage: bldr render <asset> <output> [--data '{"frame":42}']

            queues a render against the current snapshot and prints the result.
            """,
        ["renders"] = "usage: bldr renders [asset] [-n 20]\n\nrecent render jobs with the snapshot version that handled them",
        ["replay"] = "usage: bldr replay <renderId>\n\nre-runs a past render against the current snapshot",
    };
}

public static class Commands
{
    public static async Task<int> RunAsync(string[] args, TextWriter @out, HttpClient? http = null)
    {
        var stdout = @out;
        var parsed = ArgsParser.Parse(args);
        var config = BldrConfig.Load();
        Console.Error.WriteLine($"[cfg] path={BldrConfig.Path} session={(config.Session is null ? "null" : "set")} verb={args.FirstOrDefault()}");
        using var api = new BldrApi(config, http);

        try
        {
            if (parsed.Verb is null || parsed.Help)
            {
                if (parsed.Verb is not null && Manual.PerCommand.TryGetValue(parsed.Verb, out var perCmdHelp))
                    stdout.WriteLine(Out.D(perCmdHelp));
                else
                    stdout.WriteLine(Manual.Overview);
                return parsed.Verb is null ? 0 : 0;
            }

            if (parsed.Verb is "version")
            {
                stdout.WriteLine("bldr 1.0.0");
                return 0;
            }

            if (Manual.PerCommand.TryGetValue(parsed.Verb, out var cmdHelp) && parsed.Help)
            {
                stdout.WriteLine(Out.D(cmdHelp));
                return 0;
            }

            return parsed.Verb switch
            {
                "login" => await LoginAsync(api, config, parsed, stdout),
                "logout" => await LogoutAsync(api, config, stdout),
                "whoami" => await WhoamiAsync(api, stdout),
                "projects" => await ProjectsAsync(api, stdout),
                "init" => await InitAsync(api, config, parsed, stdout),
                "use" => await UseAsync(api, config, parsed, stdout),
                "status" => await StatusAsync(api, config, stdout),
                "assets" => await AssetsAsync(api, config, stdout),
                "add" => await AddAsync(api, config, parsed, stdout),
                "edit" => await EditAsync(api, config, parsed, stdout),
                "commit" => await CommitAsync(api, config, parsed, stdout),
                "discard" => await DiscardAsync(api, config, parsed, stdout),
                "log" => await LogAsync(api, config, parsed, stdout),
                "diff" => await DiffAsync(api, config, parsed, stdout),
                "rollback" => await RollbackAsync(api, config, parsed, stdout),
                "rm" => await RemoveAsync(api, config, parsed, stdout),
                "render" => await RenderAsync(api, config, parsed, stdout),
                "renders" => await RendersAsync(api, config, parsed, stdout),
                "replay" => await ReplayAsync(api, config, parsed, stdout),
                _ => Unknown(parsed.Verb, stdout),
            };
        }
        catch (BldrException ex)
        {
            stdout.WriteLine(Out.R("✗ " + ex.Message));
            return 1;
        }
        catch (HttpRequestException ex)
        {
            stdout.WriteLine(Out.R("✗ cannot reach the weft server — " + ex.Message));
            return 1;
        }
    }

    private static int Unknown(string verb, TextWriter stdout)
    {
        stdout.WriteLine(Out.R($"✗ unknown command '{verb}'"));
        stdout.WriteLine(Out.D("run 'bldr help' to see what bldr can do"));
        return 2;
    }

    // ── session ─────────────────────────────────────────────────────────────
    private static async Task<int> LoginAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (args.Get("--url") is { } url) config.ServerUrl = url.TrimEnd('/');

        var email = args.Get("--email", "-u");
        var password = args.Get("--password", "-p");

        if (email is null)
        {
            o.Write(Out.D("email: "));
            email = Console.ReadLine()?.Trim() ?? "";
        }
        if (password is null)
        {
            o.Write(Out.D("password: "));
            password = ReadHidden();
        }

        var me = await api.LoginAsync(email, password);
        config.Save();
        o.WriteLine(Out.G($"✓ signed in as {Out.S(me["email"])} ({Out.S(me["role"])})"));
        o.WriteLine(Out.D($"  server: {config.ServerUrl} — session stored in {BldrConfig.Path}"));
        return 0;
    }

    private static string ReadHidden()
    {
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (key.Key is ConsoleKey.Backspace && sb.Length > 0) { sb.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
        }
        return sb.ToString();
    }

    private static async Task<int> LogoutAsync(BldrApi api, BldrConfig config, TextWriter o)
    {
        await api.LogoutAsync();
        o.WriteLine(Out.G("✓ signed out"));
        return 0;
    }

    private static async Task<int> WhoamiAsync(BldrApi api, TextWriter o)
    {
        var me = await api.MeAsync();
        o.WriteLine($"{Out.B(Out.S(me["displayName"]))} <{Out.S(me["email"])}> — {Out.C(Out.S(me["role"]))}");
        return 0;
    }

    // ── productions ─────────────────────────────────────────────────────────
    private static async Task<int> ProjectsAsync(BldrApi api, TextWriter o)
    {
        var projects = (await api.ProjectsAsync()).AsArray();
        foreach (var p in projects)
        {
            var marker = Out.G("●");
            o.WriteLine($"  {marker} {Out.B(Out.S(p!["name"]))}  {Out.D($"{Out.I(p["assetCount"])} assets")}  {Out.D(Out.S(p["description"]))}");
        }
        return 0;
    }

    private static async Task<int> InitAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var name = args.Operands.FirstOrDefault()
            ?? throw new BldrException("usage: bldr init <name> [--description \"...\"]", 2);
        var created = await api.CreateProjectAsync(name, args.Get("--description") ?? "");
        config.ProjectId = Guid.Parse(Out.S(created["id"]));
        config.ProjectName = Out.S(created["name"]);
        config.Save();
        o.WriteLine(Out.G($"✓ production '{config.ProjectName}' created and set active"));
        o.WriteLine(Out.D("  next: bldr add <asset> to put something under version control"));
        return 0;
    }

    private static async Task<int> UseAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (args.Operands.FirstOrDefault() is not { } name)
        {
            o.WriteLine(config.ProjectId is null
                ? Out.Y("no active production — run: bldr use <name>")
                : $"active production: {Out.B(config.ProjectName)}");
            return 0;
        }

        var projects = (await api.ProjectsAsync()).AsArray();
        var match = projects.FirstOrDefault(p =>
            string.Equals(Out.S(p!["name"]), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new BldrException($"no production named '{name}' — run: bldr projects");

        config.ProjectId = Guid.Parse(Out.S(match["id"]));
        config.ProjectName = Out.S(match["name"]);
        config.Save();
        o.WriteLine(Out.G($"✓ active production: {config.ProjectName}"));
        return 0;
    }

    private static (Guid Id, string Name) RequireProject(BldrConfig config)
    {
        if (config.ProjectId is null)
            throw new BldrException("no active production — run: bldr use <name>");
        return (config.ProjectId.Value, config.ProjectName ?? "");
    }

    private static async Task<int> StatusAsync(BldrApi api, BldrConfig config, TextWriter o)
    {
        var (projectId, projectName) = RequireProject(config);
        o.WriteLine(Out.B(projectName) + Out.D("  (active production)"));
        o.WriteLine();

        var assets = (await api.AssetsAsync(projectId)).AsArray();
        if (assets.Count == 0)
        {
            o.WriteLine(Out.D("  no assets yet — run: bldr add <name>"));
            return 0;
        }

        foreach (var a in assets)
        {
            var active = a!["isActive"]!.GetValue<bool>();
            var dirty = a["dirty"]!.GetValue<bool>();
            var marker = active ? Out.G("●") : Out.D("○");
            var state = dirty ? Out.Y($"draft · {a["version"]!.GetValue<int>()}→?") : Out.G($"in sync v{a["version"]!.GetValue<int>()}");
            o.WriteLine($"  {marker} {Out.B(Out.S(a["name"]))}  {Out.D(Out.S(a["hash"]))}  {state}  " +
                        Out.D($"{Out.I(a["renderCount"])} renders · {Out.Dbl(a["successRate"]):0}% ok"));
        }

        var dirtyCount = assets.Count(a => a!["dirty"]!.GetValue<bool>());
        o.WriteLine();
        o.WriteLine(dirtyCount > 0
            ? $"  {dirtyCount} asset(s) have uncommitted changes — run: bldr commit -m \"…\""
            : Out.D("  everything committed — clean as a freshly baked blend file"));
        return 0;
    }

    private static async Task<int> AssetsAsync(BldrApi api, BldrConfig config, TextWriter o)
    {
        var (projectId, _) = RequireProject(config);
        foreach (var a in (await api.AssetsAsync(projectId)).AsArray())
            o.WriteLine($"  {Out.B(Out.S(a!["name"]))}  {Out.D(Out.S(a["sourceFile"]))}");
        return 0;
    }

    // ── add ─────────────────────────────────────────────────────────────────
    private static async Task<int> AddAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var (projectId, _) = RequireProject(config);
        var raw = args.Operands.FirstOrDefault()
            ?? throw new BldrException("usage: bldr add <name|.|path> [--source //pool/file.blend]", 2);

        var name = raw == "." ? Path.GetFileName(Directory.GetCurrentDirectory()) : raw;
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        var source = args.Get("--source") ?? $"//{slug}/{slug}.blend";
        var engine = (args.Get("--engine") ?? "CYCLES").ToUpperInvariant();
        if (engine is not ("CYCLES" or "EEVEE"))
            throw new BldrException("engine must be CYCLES or EEVEE", 2);

        int resX = 1920, resY = 1080;
        if (args.Get("--res") is { } res)
        {
            var parts = res.ToLowerInvariant().Split('x', '×');
            if (parts.Length != 2 || !int.TryParse(parts[0], out resX) || !int.TryParse(parts[1], out resY))
                throw new BldrException("--res must look like 1920x1080", 2);
        }

        var outputs = (args.Get("--outputs") ?? "final")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct().ToList();
        if (outputs.Count == 0) outputs = ["final"];

        var configJson =
            $$"""
            {
              "sourceFile": {{Ser(source)}},
              "description": {{Ser(args.Get("--description") ?? "")}},
              "blenderVersion": "4.2.3",
              "engine": "{{engine}}",
              "resX": {{resX}},
              "resY": {{resY}},
              "render": { "samples": {{args.Get("--samples") ?? "128"}}, "denoise": true },
              "outputs": {{System.Text.Json.JsonSerializer.Serialize(outputs)}},
              "materials": {},
              "nodeToken": "",
              "outputPattern": "",
              "isActive": true
            }
            """;

        var created = await api.CreateAssetAsync(projectId, name, RegexWhitespace(configJson));
        o.WriteLine(Out.G($"✓ {Out.S(created["name"])} under version control"));
        o.WriteLine($"  v1 {Out.S(created["headHash"])} — Initial commit  {Out.D(Out.S(created["workingConfig"]!["sourceFile"]))}");
        return 0;
    }

    // ── edit / commit / discard / history ───────────────────────────────────
    private static async Task<Guid> ResolveAssetAsync(BldrApi api, BldrConfig config, string? nameOrPrefix)
    {
        var (projectId, _) = RequireProject(config);
        if (nameOrPrefix is null)
            throw new BldrException("name the asset, e.g.: bldr commit Bella -m \"…\"", 2);

        var assets = (await api.AssetsAsync(projectId)).AsArray();
        var matches = assets.Where(a =>
            string.Equals(Out.S(a!["name"]), nameOrPrefix, StringComparison.OrdinalIgnoreCase)
            || Out.S(a["name"]).StartsWith(nameOrPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

        return matches.Count switch
        {
            0 => throw new BldrException($"no asset matching '{nameOrPrefix}' — run: bldr assets"),
            1 => Guid.Parse(Out.S(matches[0]["id"])),
            _ => throw new BldrException($"'{nameOrPrefix}' is ambiguous: " +
                string.Join(", ", matches.Select(m => Out.S(m["name"])))),
        };
    }

    private static async Task<int> EditAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var assetId = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        var detail = await api.AssetAsync(assetId);
        var configNode = detail["workingConfig"]!;

        var changed = 0;
        void Set(string field, JsonNode value)
        {
            var old = configNode[field]?.ToJsonString();
            configNode[field] = value;
            if (old != configNode[field]?.ToJsonString()) changed++;
        }

        if (args.Get("--source") is { } src) Set("sourceFile", src);
        if (args.Get("--description") is { } desc) Set("description", desc);
        if (args.Get("--engine") is { } engine)
        {
            engine = engine.ToUpperInvariant();
            if (engine is not ("CYCLES" or "EEVEE"))
                throw new BldrException("engine must be CYCLES or EEVEE", 2);
            Set("engine", engine);
        }
        if (args.Get("--samples") is { } samples)
        {
            if (!int.TryParse(samples, out var n)) throw new BldrException("--samples needs a number", 2);
            configNode["render"]!["samples"] = n;
            changed++;
        }
        if (args.Get("--denoise") is { } denoise)
        {
            configNode["render"]!["denoise"] = denoise is "on" or "true";
            changed++;
        }
        if (args.Get("--res") is { } res)
        {
            var parts = res.ToLowerInvariant().Split('x', '×');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var rx) || !int.TryParse(parts[1], out var ry))
                throw new BldrException("--res must look like 1920x1080", 2);
            configNode["resX"] = rx;
            configNode["resY"] = ry;
            changed++;
        }
        if (args.Get("--enable") is { } enable)
        {
            var outputs = new JsonArray();
            foreach (var existing in configNode["outputs"]!.AsArray()) outputs.Add(existing?.DeepClone());
            foreach (var output in enable.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!outputs.Any(x => Out.S(x) == output)) outputs.Add(output);
            configNode["outputs"] = outputs;
            changed++;
        }
        if (args.Get("--disable") is { } disable)
        {
            var drop = disable.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var kept = new JsonArray();
            foreach (var existing in configNode["outputs"]!.AsArray())
                if (!drop.Contains(Out.S(existing))) kept.Add(existing?.DeepClone());
            configNode["outputs"] = kept;
            changed++;
        }
        if (args.Get("--material") is { } material)
        {
            var eq = material.IndexOf('=');
            if (eq <= 0) throw new BldrException("--material expects name=shader", 2);
            var name = material[..eq].Trim();
            var shader = material[(eq + 1)..].Trim();
            var mats = new JsonObject();
            foreach (var (k, v) in configNode["materials"]!.AsObject()) mats[k] = v?.DeepClone();
            mats[name] = shader;
            configNode["materials"] = mats;
            changed++;
        }
        if (args.Flag("--lock")) { Set("isActive", false); }
        if (args.Flag("--unlock")) { Set("isActive", true); }

        if (changed == 0)
            throw new BldrException("no changes given — run: bldr help edit", 2);

        await api.SaveConfigAsync(assetId, configNode.ToJsonString());
        o.WriteLine(Out.G($"✓ draft updated — {changed} change(s)"));
        o.WriteLine(Out.D("  review with: bldr diff " + args.Operands.First() + "   commit with: bldr commit"));
        return 0;
    }

    private static async Task<int> CommitAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var message = args.Get("--message", "-m");
        var targets = new List<(Guid Id, string Name)>();

        if (args.Flag("-a", "--all"))
        {
            var (projectId, _) = RequireProject(config);
            foreach (var a in (await api.AssetsAsync(projectId)).AsArray())
                if (a!["dirty"]!.GetValue<bool>())
                    targets.Add((Guid.Parse(Out.S(a["id"])), Out.S(a["name"])));
            if (targets.Count == 0) { o.WriteLine(Out.D("nothing to commit — no dirty assets")); return 0; }
        }
        else if (args.Operands.FirstOrDefault() is { } name)
        {
            var id = await ResolveAssetAsync(api, config, name);
            targets.Add((id, name));
        }
        else
        {
            var (projectId, _) = RequireProject(config);
            var dirty = (await api.AssetsAsync(projectId)).AsArray()
                .Where(a => a!["dirty"]!.GetValue<bool>())
                .Select(a => (Guid.Parse(Out.S(a["id"])), Out.S(a["name"])))
                .ToList();

            if (dirty.Count == 0) { o.WriteLine(Out.D("nothing to commit — everything is in sync")); return 0; }
            if (dirty.Count > 1)
            {
                o.WriteLine(Out.Y($"{dirty.Count} assets have drafts: {string.Join(", ", dirty.Select(d => d.Item2))}"));
                o.WriteLine(Out.D("commit one by name, or all with: bldr commit -a"));
                return 2;
            }
            targets = dirty;
        }

        foreach (var (id, name) in targets)
        {
            var commit = await api.CommitAsync(id, string.IsNullOrWhiteSpace(message) ? null : message.Trim());
            if (commit["version"] is null)
            {
                // 409 — the server says the draft matches HEAD
                o.WriteLine(Out.D("nothing to commit - " + name + " matches HEAD"));
                continue;
            }
            o.WriteLine(Out.G($"✓ {name}: v{Out.I(commit["version"])} {Out.S(commit["hash"])}") + Out.D($"  {Out.S(commit["message"])}"));
        }
        return 0;
    }

    private static async Task<int> DiscardAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var id = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        var detail = await api.DiscardAsync(id);
        o.WriteLine(Out.G($"✓ draft discarded — {Out.S(detail["name"])} back in sync with v{Out.I(detail["headVersion"])}"));
        return 0;
    }

    private static async Task<int> LogAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var id = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        var take = args.Get("-n") is { } n && int.TryParse(n, out var k) ? k : 20;
        foreach (var c in (await api.CommitsAsync(id)).AsArray().Take(take))
        {
            var head = Out.I(c!["version"]) == (await api.CommitsAsync(id)).AsArray().Max(x => Out.I(x!["version"]))
                ? Out.C(" HEAD") : "";
            var revert = c["isRollback"]!.GetValue<bool>() ? Out.C($" ↺v{Out.I(c["restoredVersion"])}") : "";
            o.WriteLine($"v{Out.I(c["version"])} {Out.S(c["hash"])}  {Out.S(c["author"])}  {Out.D(Out.Rel(c["createdAt"]!.GetValue<DateTime>()))}" +
                        $"{head}{revert}  {c["message"]!.ToJsonString().Trim('"')}");
        }
        return 0;
    }

    private static async Task<int> DiffAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var id = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        int? from = args.Get("--from") is { } f && int.TryParse(f, out var fv) ? fv : null;
        int? to = args.Get("--to") is { } t && int.TryParse(t, out var tv) ? tv : null;

        var diff = await api.DiffAsync(id, from, to);
        o.WriteLine($"{Out.S(diff["from"]!["label"])} ({Out.S(diff["from"]!["hash"])})  →  {Out.S(diff["to"]!["label"])} ({Out.S(diff["to"]!["hash"])})");
        o.WriteLine();

        foreach (var ch in diff["changes"]!.AsArray())
        {
            var kind = Out.S(ch!["kind"]);
            var path = Out.S(ch["path"]);
            var line = kind switch
            {
                "added" => Out.G($"  + {path}: {Out.S(ch["to"])}"),
                "removed" => Out.R($"  - {path}: {Out.S(ch["from"])}"),
                _ => Out.Y($"  ~ {path}: {Out.S(ch["from"])} → {Out.S(ch["to"])}"),
            };
            o.WriteLine(line);
        }
        if (diff["changes"]!.AsArray().Count == 0)
            o.WriteLine(Out.D("  (no differences)"));
        return 0;
    }

    private static async Task<int> RollbackAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (args.Operands.Count < 2 || !int.TryParse(args.Operands[1].TrimStart('v'), out var version))
            throw new BldrException("usage: bldr rollback <asset> <vN> [-m \"message\"]", 2);

        var id = await ResolveAssetAsync(api, config, args.Operands[0]);
        var commit = await api.RollbackAsync(id, version, args.Get("--message", "-m"));
        o.WriteLine(Out.G($"✓ v{version} restored as v{Out.I(commit["version"])} {Out.S(commit["hash"])}") + Out.D($"  {Out.S(commit["message"])}"));
        return 0;
    }

    private static async Task<int> RemoveAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (!args.Flag("--yes", "-y"))
            throw new BldrException("rm removes an asset and its entire history — confirm with --yes", 2);
        var id = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        await api.DeleteAssetAsync(id);
        o.WriteLine(Out.G("✓ removed"));
        return 0;
    }

    // ── renders ─────────────────────────────────────────────────────────────
    private static async Task<int> RenderAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (args.Operands.Count < 2)
            throw new BldrException("usage: bldr render <asset> <output> [--data '{\"frame\":1}']", 2);
        var id = await ResolveAssetAsync(api, config, args.Operands[0]);
        var output = args.Operands[1];
        var jobData = args.Get("--data") ?? "{}";

        var r = await api.QueueRenderAsync(id, output, jobData);
        var status = Out.S(r["status"]);
        var line = $"  {Out.S(r["output"])} — {status} on v{Out.I(r["assetVersion"])}  {Out.D($"{duration(Out.I(r["durationMs"]))}, {Out.I(r["attempts"])} attempt(s)")}";
        o.WriteLine(status switch
        {
            "completed" => Out.G("✓") + line,
            "blocked" => Out.Y("⊘") + line + (Out.S(r["error"]) is { Length: > 0 } e ? Out.D($"  {e}") : ""),
            _ => Out.R("✕") + line + (Out.S(r["error"]) is { Length: > 0 } e2 ? Out.R($"  {e2}") : ""),
        });
        return status is "completed" or "failed" or "blocked" ? 0 : 0;
    }

    private static async Task<int> RendersAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        var id = await ResolveAssetAsync(api, config, args.Operands.FirstOrDefault());
        var take = args.Get("-n") is { } n && int.TryParse(n, out var k) ? k : 20;
        foreach (var r in (await api.RendersAsync(id, take)).AsArray())
        {
            var icon = Out.S(r!["status"]) switch
            {
                "completed" => Out.G("✓"),
                "failed" => Out.R("✕"),
                _ => Out.Y("⊘"),
            };
            o.WriteLine($"{icon} {Out.S(r["output"])}  v{Out.I(r["assetVersion"])}  {duration(Out.I(r["durationMs"]))}  " +
                        Out.D($"{Out.Rel(r["createdAt"]!.GetValue<DateTime>())}" +
                              (Out.S(r["error"]) is { Length: > 0 } e ? $" · {e}" : "")));
        }
        return 0;
    }

    private static async Task<int> ReplayAsync(BldrApi api, BldrConfig config, ParsedArgs args, TextWriter o)
    {
        if (args.Operands.FirstOrDefault() is not { } idText || !Guid.TryParse(idText, out var renderId))
            throw new BldrException("usage: bldr replay <renderId>", 2);

        var r = await api.ReplayAsync(renderId);
        var status = Out.S(r["status"]);
        o.WriteLine($"{(status == "completed" ? Out.G("✓") : Out.R("✕"))} re-rendered {Out.S(r["output"])} on v{Out.I(r["assetVersion"])} — {status}");
        return 0;
    }

    private static string duration(int ms) => Out.Duration(ms);
    private static string Ser(string? s) => System.Text.Json.JsonSerializer.Serialize(s);
    private static string RegexWhitespace(string s) => System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ");
}
