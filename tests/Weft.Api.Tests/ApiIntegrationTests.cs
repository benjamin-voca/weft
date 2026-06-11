using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Weft.Api.Data;
using Xunit;

namespace Weft.Api.Tests;

/// <summary>
/// End-to-end API flows over the real Minimal API pipeline (SQLite backend):
/// seed → create → draft → commit → diff → rollback → renders → replay → stats.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WeftApiFactory>
{
    private readonly WeftApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public ApiIntegrationTests(WeftApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Cookie sessions persist on this client — log in as the seeded admin once.
        var res = _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.com", password = "admin" }).GetAwaiter().GetResult();
        res.EnsureSuccessStatusCode();
    }

    private WeftDb Db => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<WeftDb>();

    // ── health & seed ───────────────────────────────────────────────────────

    [Fact]
    public async Task Health_is_ok()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Seeder_created_three_productions_with_assets()
    {
        var projects = await GetJson<JsonNode[]>("/api/projects");
        Assert.Equal(3, projects.Length);
        Assert.All(projects, p => Assert.True(p["assetCount"]!.GetValue<int>() > 0));

        var first = projects[0]["id"]!.GetValue<string>();
        var assets = await GetJson<JsonNode[]>($"/api/projects/{first}/assets");
        Assert.NotEmpty(assets);
    }

    // ── the core VCS lifecycle ──────────────────────────────────────────────

    [Fact]
    public async Task Create_edit_commit_diff_rollback_flow()
    {
        var projectId = (await GetJson<JsonNode[]>("/api/projects"))[0]["id"]!.GetValue<string>();

        // create → v1 "Initial commit"
        var createRes = await _client.PostAsJsonAsync($"/api/projects/{projectId}/assets", new
        {
            name = "Test Asset",
            config = ConfigJson()
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var asset = await createRes.Content.ReadFromJsonAsync<JsonNode>();
        var assetId = asset!["id"]!.GetValue<string>();
        Assert.Equal(1, asset["headVersion"]!.GetValue<int>());
        Assert.False(asset["dirty"]!.GetValue<bool>());

        // edit draft → dirty with field-level diff
        var config = ConfigJson();
        config["sourceFile"] = "//characters/bella/bella_rig_v015.blend";
        config["render"]!["samples"] = 256;
        config["outputs"]!.AsArray().Add("turntable");

        await _client.PutAsJsonAsync($"/api/assets/{assetId}/config", new { config });

        var draft = await GetJson<JsonNode>($"/api/assets/{assetId}");
        Assert.True(draft["dirty"]!.GetValue<bool>());
        Assert.Equal(3, draft["draftDiff"]!.AsArray().Count);

        // committing with no message auto-generates one from the diff
        var commitRes = await _client.PostAsJsonAsync($"/api/assets/{assetId}/commit", new { message = (string?)null });
        Assert.Equal(HttpStatusCode.Created, commitRes.StatusCode);
        var commit = await commitRes.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal(2, commit!["version"]!.GetValue<int>());
        Assert.Matches("^[0-9a-f]{8}$", commit["hash"]!.GetValue<string>());
        Assert.Contains("Samples 128 → 256", commit["message"]!.GetValue<string>());

        // in sync after commit; committing again is a conflict ("nothing to commit")
        var after = await GetJson<JsonNode>($"/api/assets/{assetId}");
        Assert.False(after["dirty"]!.GetValue<bool>());
        var noop = await _client.PostAsJsonAsync($"/api/assets/{assetId}/commit", new { message = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, noop.StatusCode);

        // diff any two versions
        var diff = await GetJson<JsonNode>($"/api/assets/{assetId}/diff?from=1&to=2");
        Assert.Equal(3, diff["changes"]!.AsArray().Count);
        Assert.Equal("v1", diff["from"]!["label"]!.GetValue<string>());

        // rollback → history grows, never rewrites
        var rollbackRes = await _client.PostAsJsonAsync($"/api/assets/{assetId}/rollback", new { version = 1, message = (string?)null });
        Assert.Equal(HttpStatusCode.OK, rollbackRes.StatusCode);
        var revert = await rollbackRes.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal(3, revert!["version"]!.GetValue<int>());
        Assert.True(revert["isRollback"]!.GetValue<bool>());
        Assert.Equal(1, revert["restoredVersion"]!.GetValue<int>());

        var detail = await GetJson<JsonNode>($"/api/assets/{assetId}");
        Assert.Equal("//characters/bella/bella_rig_v014.blend",
            detail["workingConfig"]!["sourceFile"]!.GetValue<string>());

        var rollbackToHead = await _client.PostAsJsonAsync($"/api/assets/{assetId}/rollback", new { version = 3, message = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, rollbackToHead.StatusCode);

        var history = await GetJson<JsonNode[]>($"/api/assets/{assetId}/commits");
        Assert.Equal(3, history.Length); // v1, v2, v3 — v2 was not rewritten
    }

    [Fact]
    public async Task Discard_restores_head_into_draft()
    {
        var assetId = await CreateAssetAsync();

        var config = ConfigJson();
        config["description"] = "draft edit that will be discarded";
        await _client.PutAsJsonAsync($"/api/assets/{assetId}/config", new { config });

        var discarded = await (await _client.PostAsync($"/api/assets/{assetId}/discard", null))
            .Content.ReadFromJsonAsync<JsonNode>();
        Assert.False(discarded!["dirty"]!.GetValue<bool>());
        Assert.Equal("hero character rig", discarded["workingConfig"]!["description"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invalid_config_is_rejected_with_reason()
    {
        var projectId = (await GetJson<JsonNode[]>("/api/projects"))[0]["id"]!.GetValue<string>();
        var config = ConfigJson();
        config["sourceFile"] = "not-a-blend-file.fbx";
        var res = await _client.PostAsJsonAsync($"/api/projects/{projectId}/assets", new { name = "Bad", config });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Contains(".blend", body!["error"]!.GetValue<string>());
    }

    // ── render jobs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Queued_render_pins_head_version_and_validates_output()
    {
        var assetId = await CreateAssetAsync();

        var ok = await (await _client.PostAsJsonAsync($"/api/assets/{assetId}/renders", new
        {
            output = "final",
            jobData = (JsonNode)JsonNode.Parse("""{"frame":42,"camera":"closeup"}""")!
        })).Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("completed", ok!["status"]!.GetValue<string>());
        Assert.Equal(1, ok["assetVersion"]!.GetValue<int>());

        var disabled = await _client.PostAsJsonAsync($"/api/assets/{assetId}/renders", new
        {
            output = "turntable",
            jobData = (JsonNode)JsonNode.Parse("{}")!
        });
        var blocked = await disabled.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("blocked", blocked!["status"]!.GetValue<string>());
        Assert.Contains("not enabled", blocked["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Forced_failure_and_replay_against_current_config()
    {
        var assetId = await CreateAssetAsync();

        var res = await _client.PostAsJsonAsync($"/api/assets/{assetId}/renders", new
        {
            output = "final",
            jobData = (JsonNode)JsonNode.Parse("""{"frame":7,"force":"fail"}""")!
        });
        var failed = await res.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("failed", failed!["status"]!.GetValue<string>());
        var renderId = failed["id"]!.GetValue<string>();

        var replayRes = await (await _client.PostAsync($"/api/renders/{renderId}/replay", null))
            .Content.ReadFromJsonAsync<JsonNode>();
        Assert.True(replayRes!["isReplay"]!.GetValue<bool>());
        Assert.Equal(renderId, replayRes["replayOfId"]!.GetValue<string>());

        var log = await GetJson<JsonNode[]>($"/api/assets/{assetId}/renders");
        Assert.True(log.Length >= 2);
    }

    [Fact]
    public async Task Locked_asset_blocks_renders()
    {
        var projectId = (await GetJson<JsonNode[]>("/api/projects"))[0]["id"]!.GetValue<string>();
        var config = ConfigJson();
        config["isActive"] = false;
        var create = await _client.PostAsJsonAsync($"/api/projects/{projectId}/assets", new { name = "Locked", config });
        var asset = await create.Content.ReadFromJsonAsync<JsonNode>();
        var assetId = asset!["id"]!.GetValue<string>();

        var res = await _client.PostAsJsonAsync($"/api/assets/{assetId}/renders", new
        {
            output = "final",
            jobData = (JsonNode)JsonNode.Parse("{}")!
        });
        var render = await res.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("blocked", render!["status"]!.GetValue<string>());
        Assert.Contains("locked", render["error"]!.GetValue<string>());
    }

    // ── stats & deletion ────────────────────────────────────────────────────

    [Fact]
    public async Task Stats_aggregate_the_project()
    {
        var projectId = (await GetJson<JsonNode[]>("/api/projects"))[0]["id"]!.GetValue<string>();
        var stats = await GetJson<JsonNode>($"/api/projects/{projectId}/stats");

        var totals = stats["totals"]!;
        Assert.Equal(totals["completed"]!.GetValue<int>()
            + totals["failed"]!.GetValue<int>()
            + totals["blocked"]!.GetValue<int>(),
            totals["renders"]!.GetValue<int>());
        Assert.Equal(14, stats["series"]!.AsArray().Count);
        Assert.NotEmpty(stats["assets"]!.AsArray());
    }

    [Fact]
    public async Task Deleting_an_asset_cascades()
    {
        var assetId = await CreateAssetAsync();
        var res = await _client.DeleteAsync($"/api/assets/{assetId}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/assets/{assetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/assets/{assetId}/commits")).StatusCode);

        var gone = await Db.Assets.FirstOrDefaultAsync(a => a.Id == Guid.Parse(assetId));
        Assert.Null(gone);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static JsonObject ConfigJson() => new()
    {
        ["sourceFile"] = "//characters/bella/bella_rig_v014.blend",
        ["description"] = "hero character rig",
        ["blenderVersion"] = "4.2.3",
        ["engine"] = "CYCLES",
        ["resX"] = 1920,
        ["resY"] = 1080,
        ["render"] = new JsonObject { ["samples"] = 128, ["denoise"] = true },
        ["outputs"] = new JsonArray("final"),
        ["materials"] = new JsonObject { ["Bella Skin"] = "Principled BSDF" },
        ["nodeToken"] = "rnd_test",
        ["outputPattern"] = "",
        ["isActive"] = true
    };

    private async Task<string> CreateAssetAsync()
    {
        var projectId = (await GetJson<JsonNode[]>("/api/projects"))[0]["id"]!.GetValue<string>();
        var res = await _client.PostAsJsonAsync($"/api/projects/{projectId}/assets", new
        {
            name = "Asset " + Guid.NewGuid().ToString("N")[..6],
            config = ConfigJson()
        });
        var asset = await res.Content.ReadFromJsonAsync<JsonNode>();
        return asset!["id"]!.GetValue<string>();
    }

    private async Task<T> GetJson<T>(string url)
    {
        var res = await _client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
