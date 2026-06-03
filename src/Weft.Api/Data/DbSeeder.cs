using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Common;
using Weft.Api.Data;
using Weft.Api.Domain;
using Weft.Api.Versioning;

namespace Weft.Api.Data;

/// <summary>
/// Deterministic seed: three Blender productions with commit histories,
/// pending drafts, a broken bake source that keeps failing, a locked set
/// piece, and three weeks of render traffic whose failure clusters line up
/// with config versions (the "blame" story).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(WeftDb db)
    {
        await EnsureAdminAsync(db);
        if (await db.Projects.AnyAsync()) return;

        var rnd = new Random(20260918);
        var now = DateTime.UtcNow;

        var coffeeRun = new Project
        {
            Name = "Coffee Run",
            Description = "Animated short — two baristas, one runaway espresso machine.",
            CreatedAt = now.AddDays(-30)
        };
        var nordic = new Project
        {
            Name = "Nordic Kitchen",
            Description = "Arch-viz stills and walkthroughs for a modular kitchen client.",
            CreatedAt = now.AddDays(-24)
        };
        var ironvale = new Project
        {
            Name = "Ironvale",
            Description = "Stylized game-asset pipeline for an indie RPG.",
            CreatedAt = now.AddDays(-18)
        };

        // ── Coffee Run / Bella — Character Rig ──────────────────────────────
        var bella = NewHook(coffeeRun, "Bella — Character Rig", now.AddDays(-21));
        var bellaV1 = new AssetConfig
        {
            SourceFile = "//characters/bella/bella_rig_v014.blend",
            Description = "Hero character rig — deformation tested to 4K.",
            BlenderVersion = "4.2.3",
            Engine = "CYCLES",
            ResX = 1920, ResY = 1080,
            Render = new RenderSettings { Samples = 128, Denoise = false },
            Outputs = ["viewport"],
            Materials = new() { ["Bella Skin"] = "Principled BSDF", ["Coat Fabric"] = "Principled BSDF" },
            NodeToken = "rnd_e8Kq2mA9",
            OutputPattern = "//renders/coffee_run/bella/v{{version}}/{{output}}_####"
        };
        AddCommit(bella, bellaV1, "klajdi", now.AddDays(-21));
        AddCommit(bella, With(bellaV1, outputs: ["viewport", "final"]), "klajdi", now.AddDays(-19));
        var bellaV3 = With(bellaV1, outputs: ["viewport", "final"], render: new RenderSettings { Samples = 256, Denoise = true });
        AddCommit(bella, bellaV3, "marcus", now.AddDays(-9), custom: "Fix noise on hero closeups");
        AddCommit(bella, With(bellaV3, nodeToken: "rnd_Lm83Tq0z"), "klajdi", now.AddDays(-2));
        bella.WorkingConfig = With(bella.WorkingConfig, outputs: ["viewport", "final", "turntable"]);
        SeedTraffic(rnd, bella, now, perDay: 5, failureWindow: (-8, -6), payloadKind: "character");

        // ── Coffee Run / Harbor Street — Environment ────────────────────────
        var harbor = NewHook(coffeeRun, "Harbor Street — Environment", now.AddDays(-20));
        var harborV1 = new AssetConfig
        {
            SourceFile = "//environments/harbor/harbor_master.blend",
            Description = "Full CG harbor street — hero establishing shots.",
            BlenderVersion = "4.2.3",
            Engine = "EEVEE",
            ResX = 1920, ResY = 1080,
            Render = new RenderSettings { Samples = 64, Denoise = true },
            Outputs = ["final", "viewport"],
            Materials = new() { ["Harbor Water"] = "Principled BSDF" },
            NodeToken = "rnd_Hr5Tt1Wq",
            OutputPattern = "//renders/coffee_run/harbor/v{{version}}/{{output}}_####"
        };
        AddCommit(harbor, harborV1, "klajdi", now.AddDays(-20));
        AddCommit(harbor, With(harborV1, resX: 2560, resY: 1440), "ci-bot", now.AddDays(-16),
            custom: "Bump deliverables to 2K as per client email");
        AddCommit(harbor, With(harborV1, resX: 2560, resY: 1440,
            materials: new() { ["Harbor Water"] = "Principled BSDF", ["Wet Cobblestone"] = "Principled BSDF" }),
            "dana.p", now.AddDays(-7));
        SeedTraffic(rnd, harbor, now, perDay: 3, transientRate: 8, payloadKind: "environment");

        // ── Coffee Run / Old Lighthouse — Set Piece (locked) ────────────────
        var lighthouse = NewHook(coffeeRun, "Old Lighthouse — Set Piece", now.AddDays(-14));
        var lightV1 = new AssetConfig
        {
            SourceFile = "//environments/lighthouse/lighthouse.blend",
            Description = "Background set piece — distant shots only.",
            BlenderVersion = "4.1.1",
            Engine = "EEVEE",
            ResX = 1920, ResY = 1080,
            Render = new RenderSettings { Samples = 32, Denoise = true },
            Outputs = ["final"],
            NodeToken = "rnd_Lh9Rr2Nz"
        };
        AddCommit(lighthouse, lightV1, "dana.p", now.AddDays(-14));
        AddCommit(lighthouse, With(lightV1,
            materials: new() { ["Weathered Brick"] = "Principled BSDF" }), "marcus", now.AddDays(-10));
        AddCommit(lighthouse, With(lightV1, materials: new() { ["Weathered Brick"] = "Principled BSDF" },
            isActive: false), "marcus", now.AddDays(-5), custom: "Lock until texture rework lands");
        SeedTraffic(rnd, lighthouse, now, perDay: 1, pausedAt: now.AddDays(-5), payloadKind: "environment");

        // ── Nordic Kitchen / Kitchen Master Scene ───────────────────────────
        var kitchen = NewHook(nordic, "Kitchen Master Scene", now.AddDays(-18));
        var kitchenV1 = new AssetConfig
        {
            SourceFile = "//scenes/kitchen/kitchen_master_v008.blend",
            Description = "Hero scene for client stills and the walkthrough.",
            BlenderVersion = "4.2.3",
            Engine = "CYCLES",
            ResX = 3840, ResY = 2160,
            Render = new RenderSettings { Samples = 512, Denoise = true },
            Outputs = ["final", "viewport"],
            Materials = new() { ["Cabinet Oak"] = "Principled BSDF", ["Counter Marble"] = "Principled BSDF" },
            NodeToken = "rnd_Nk3Pp7Qs",
            OutputPattern = "//renders/nordic/kitchen/v{{version}}/{{output}}_####"
        };
        AddCommit(kitchen, kitchenV1, "klajdi", now.AddDays(-18));
        AddCommit(kitchen, With(kitchenV1, engine: "EEVEE",
            render: new RenderSettings { Samples = 64, Denoise = true }), "dana.p", now.AddDays(-12),
            custom: "EEVEE pass for the client preview meeting");
        AddCommit(kitchen, With(kitchenV1, render: new RenderSettings { Samples = 512, Denoise = true }),
            "klajdi", now.AddDays(-6), custom: "Back to Cycles quality for final stills");
        SeedTraffic(rnd, kitchen, now, perDay: 4, transientRate: 6, payloadKind: "scene");

        // ── Nordic Kitchen / Oak & Stone — Material Library ─────────────────
        var oak = NewHook(nordic, "Oak & Stone — Material Library", now.AddDays(-13));
        var oakV1 = new AssetConfig
        {
            SourceFile = "//materials/oak_stone_library.blend",
            Description = "Shared shader library — linked into every Nordic scene.",
            BlenderVersion = "4.2.3",
            Engine = "EEVEE",
            ResX = 1024, ResY = 1024,
            Render = new RenderSettings { Samples = 32, Denoise = true },
            Outputs = ["bake", "thumbnail"],
            Materials = new() { ["Counter Marble"] = "Principled BSDF" },
            OutputPattern = "//textures/nordic/{{asset}}/{{material}}_####"
        };
        AddCommit(oak, oakV1, "klajdi", now.AddDays(-13));
        AddCommit(oak, With(oakV1, materials: new() { ["Counter Marble"] = "Principled BSDF", ["Oak Planks"] = "Principled BSDF" }),
            "dana.p", now.AddDays(-9));
        AddCommit(oak, With(oakV1, materials: new() { ["Counter Marble"] = "Principled BSDF", ["Oak Planks"] = "Principled BSDF v2 (roughness map)" }),
            "dana.p", now.AddDays(-3), custom: "Roughness map from the second photoscan batch");
        SeedTraffic(rnd, oak, now, perDay: 2, payloadKind: "materials");

        // ── Ironvale / Sword of Dunmarr — Legacy Bake (failing) ─────────────
        var sword = NewHook(ironvale, "Sword of Dunmarr — Legacy Bake", now.AddDays(-16));
        var swordV1 = new AssetConfig
        {
            SourceFile = "//assets/sword/broken_bake_source.blend",
            Description = "Old bake scene — linked libraries lost in the 3.6 migration.",
            BlenderVersion = "3.6.5",
            Engine = "EEVEE",
            ResX = 1024, ResY = 1024,
            Render = new RenderSettings { Samples = 64, Denoise = false },
            Outputs = ["bake"],
            OutputPattern = "//textures/ironvale/sword/{{bake_type}}_####"
        };
        AddCommit(sword, swordV1, "klajdi", now.AddDays(-16));
        AddCommit(sword, With(swordV1, render: new RenderSettings { Samples = 256, Denoise = false }),
            "marcus", now.AddDays(-8), custom: "Try heavier sampling for the bake OOM");
        AddCommit(sword, With(swordV1, sourceFile: "//assets/sword/broken_bake_source_v2.blend"),
            "marcus", now.AddDays(-4), custom: "Isolate bake scene — recovery attempt #3");
        SeedTraffic(rnd, sword, now, perDay: 2, alwaysFail: true, payloadKind: "prop");

        // ── Ironvale / Shield of the Vale ───────────────────────────────────
        var shield = NewHook(ironvale, "Shield of the Vale", now.AddDays(-11));
        var shieldV1 = new AssetConfig
        {
            SourceFile = "//assets/shield/shield_master.blend",
            Description = "Tier-2 shield — game res plus a 4K turntable.",
            BlenderVersion = "4.2.3",
            Engine = "EEVEE",
            ResX = 1920, ResY = 1080,
            Render = new RenderSettings { Samples = 64, Denoise = true },
            Outputs = ["final", "bake"],
            Materials = new() { ["Ironvale Steel"] = "Principled BSDF", ["Royal Enamel"] = "Principled BSDF" },
            NodeToken = "rnd_Sh2Vl5Dz"
        };
        AddCommit(shield, shieldV1, "klajdi", now.AddDays(-11));
        SeedTraffic(rnd, shield, now, perDay: 1, failureWindow: (-4, -3), payloadKind: "prop");

        db.Projects.AddRange(coffeeRun, nordic, ironvale);
        await db.SaveChangesAsync();
    }

    /// <summary>Main account, seeded independently of the demo data so it
    /// exists even in databases that already carry projects.</summary>
    private static async Task EnsureAdminAsync(WeftDb db)
    {
        if (await db.Users.AnyAsync(u => u.Email == "admin@example.com")) return;
        db.Users.Add(new User
        {
            Email = "admin@example.com",
            DisplayName = "Administrator",
            Role = "Admin",
            PasswordHash = PasswordHasher.Hash("admin")
        });
        await db.SaveChangesAsync();
    }

    private static Asset NewHook(Project project, string name, DateTime createdAt)
    {
        var asset = new Asset
        {
            ProjectId = project.Id,
            Name = name,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        project.Assets.Add(asset);
        return asset;
    }

    private static void AddCommit(Asset asset, AssetConfig config, string author, DateTime at,
        string? custom = null, bool rollback = false, int? restored = null)
    {
        var prev = asset.Commits.OrderBy(c => c.Version).LastOrDefault();
        var prevConfig = prev is null ? new AssetConfig() : CommitFactory.ParseSnapshot(prev.SnapshotJson);
        var changes = SemanticDiff.Diff(prevConfig, config);
        var autoMessage = prev is null ? "Initial commit" : CommitMessage.From(changes);
        asset.Commits.Add(CommitFactory.Create(asset.Id, (prev?.Version ?? 0) + 1, config, changes,
            autoMessage, author, custom, rollback, restored, at));
        asset.WorkingConfig = config;
        asset.UpdatedAt = at;
    }

    private static AssetConfig With(
        AssetConfig src,
        string? sourceFile = null, string? description = null, string? blenderVersion = null,
        string? engine = null, int? resX = null, int? resY = null, RenderSettings? render = null,
        List<string>? outputs = null, Dictionary<string, string>? materials = null,
        string? nodeToken = null, string? outputPattern = null, bool? isActive = null) => new()
    {
        SourceFile = sourceFile ?? src.SourceFile,
        Description = description ?? src.Description,
        BlenderVersion = blenderVersion ?? src.BlenderVersion,
        Engine = engine ?? src.Engine,
        ResX = resX ?? src.ResX,
        ResY = resY ?? src.ResY,
        Render = render is null
            ? new RenderSettings { Samples = src.Render.Samples, Denoise = src.Render.Denoise }
            : render,
        Outputs = outputs ?? [.. src.Outputs],
        Materials = materials is null ? new(src.Materials) : new(materials),
        NodeToken = nodeToken ?? src.NodeToken,
        OutputPattern = outputPattern ?? src.OutputPattern,
        IsActive = isActive ?? src.IsActive
    };

    private static void SeedTraffic(Random rnd, Asset asset, DateTime now, int perDay,
        (int, int)? failureWindow = null, int transientRate = 12, bool alwaysFail = false,
        DateTime? pausedAt = null, string payloadKind = "generic")
    {
        for (var d = 13; d >= 0; d--)
        {
            var count = Math.Max(1, perDay + rnd.Next(-1, 2));
            for (var i = 0; i < count; i++)
            {
                var at = now.AddDays(-d).AddHours(rnd.Next(8, 22)).AddMinutes(rnd.Next(0, 60));
                var output = PickOutput(rnd, asset);
                var jobData = JobData(payloadKind, output, rnd, at);

                string status;
                int attempts, duration;
                int? exitCode;
                string? error = null;

                if (pausedAt is not null && at >= pausedAt)
                {
                    (status, exitCode, duration, attempts, error) =
                        ("blocked", null, 0, 0, "Asset is locked");
                }
                else if (alwaysFail)
                {
                    (status, exitCode, duration, attempts, error) =
                        ("failed", 1, 600_000, 3, "GPU out of memory after 3 attempts");
                }
                else if (failureWindow is { } win && d <= -win.Item1 && d >= -win.Item2 && rnd.Next(100) < 40)
                {
                    var recovered = rnd.Next(100) < 35;
                    if (recovered) (status, exitCode, duration, attempts, error) = ("completed", 0, 310_000, 2, null);
                    else (status, exitCode, duration, attempts, error) =
                        ("failed", 1, 600_000, asset.WorkingConfig.Render.Samples / 128, "GPU out of memory");
                }
                else if (rnd.Next(100) < transientRate)
                {
                    (status, exitCode, duration, attempts, error) = ("completed", 0, 295_000, 2, null);
                }
                else
                {
                    (status, exitCode, duration, attempts, error) =
                        ("completed", 0, 25_000 + rnd.Next(280_000), 1, null);
                }

                asset.RenderJobs.Add(new RenderJob
                {
                    AssetId = asset.Id,
                    AssetVersion = VersionAt(asset, at),
                    Output = output,
                    JobDataJson = jobData,
                    Status = status,
                    ExitCode = exitCode,
                    DurationMs = duration,
                    Attempts = attempts,
                    Error = error,
                    IsReplay = rnd.Next(100) < 4,
                    CreatedAt = at
                });
            }
        }
    }

    private static int VersionAt(Asset asset, DateTime at) =>
        asset.Commits.Where(c => c.CreatedAt <= at).OrderBy(c => c.Version).LastOrDefault()?.Version ?? 0;

    private static string PickOutput(Random rnd, Asset asset) =>
        asset.WorkingConfig.Outputs[rnd.Next(asset.WorkingConfig.Outputs.Count)];

    private static string JobData(string kind, string output, Random rnd, DateTime at)
    {
        var frame = rnd.Next(1, 240);
        return kind switch
        {
            "character" => $"{{\"frame\":{frame},\"camera\":\"closeup\",\"pass\":\"beauty\",\"samples_scale\":{rnd.Next(80, 120)}%}}".Replace("%", ""),
            "environment" => $"{{\"frames\":\"{frame}-{frame + rnd.Next(24, 96)}\",\"camera\":\"drone\",\"pass\":\"beauty\"}}",
            "scene" => $"{{\"frame\":{frame},\"camera\":\"{(rnd.Next(100) < 50 ? "corner_wide" : "corner_wide_two")}\",\"pass\":\"beauty\"}}",
            "materials" => $"{{\"bake_type\":\"diffuse\",\"uv\":\"UVMap.001\",\"resolution\":1024}}",
            "prop" => output == "bake"
                ? $"{{\"bake_type\":\"{(rnd.Next(100) < 50 ? "diffuse" : "normal")}\",\"uv\":\"UVMap.001\",\"resolution\":2048}}"
                : $"{{\"frames\":\"1-72\",\"turntable_steps\":72}}",
            _ => $"{{\"frame\":{frame}}}"
        };
    }
}
