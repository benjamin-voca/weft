using Weft.Api.Domain;
using Weft.Api.Versioning;
using Xunit;

namespace Weft.Api.Tests;

public class VersioningTests
{
    private static AssetConfig Config(Action<AssetConfig>? mutate = null)
    {
        var config = new AssetConfig
        {
            SourceFile = "//characters/bella/bella_rig_v014.blend",
            Description = "hero character rig",
            BlenderVersion = "4.2.3",
            Engine = "CYCLES",
            ResX = 1920, ResY = 1080,
            Render = new RenderSettings { Samples = 128, Denoise = true },
            Outputs = ["final"],
            Materials = new() { ["Bella Skin"] = "Principled BSDF" },
            NodeToken = "rnd_abcd1234"
        };
        mutate?.Invoke(config);
        return config;
    }

    // ── canonical JSON + hashing ────────────────────────────────────────────

    [Fact]
    public void Hash_is_stable_regardless_of_property_order()
    {
        // ContentHash operates on canonical JSON, so the same content in
        // different key orders must canonicalize to the same hash.
        var a = ContentHash.Of(CanonicalJson.Serialize(System.Text.Json.Nodes.JsonNode.Parse("""{"B":"2","A":"1"}""")));
        var b = ContentHash.Of(CanonicalJson.Serialize(System.Text.Json.Nodes.JsonNode.Parse("""{"A":"1","B":"2"}""")));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_is_eight_hex_chars_and_content_sensitive()
    {
        var hash = ContentHash.Of("""{"a":1}""");
        Assert.Matches("^[0-9a-f]{8}$", hash);
        Assert.NotEqual(hash, ContentHash.Of("""{"a":2}"""));
    }

    // ── semantic diff ───────────────────────────────────────────────────────

    [Fact]
    public void Diff_detects_source_output_and_sample_changes()
    {
        var from = Config();
        var to = Config(c =>
        {
            c.SourceFile = "//characters/bella/bella_rig_v015.blend";
            c.Outputs.Add("turntable");
            c.Render.Samples = 256;
        });

        var changes = SemanticDiff.Diff(from, to);

        Assert.Contains(changes, c => c.Path == "sourceFile" && c.Kind == "modified");
        Assert.Contains(changes, c => c.Path == "outputs" && c.Kind == "added" && c.To == "turntable");
        Assert.Contains(changes, c => c.Path == "render.samples" && c.From == "128" && c.To == "256");
        Assert.Equal(3, changes.Count);
    }

    [Fact]
    public void Diff_detects_material_and_engine_changes()
    {
        var from = Config(c => c.Materials = new() { ["Keep"] = "Principled BSDF", ["Old Skin"] = "Subsurface" });
        var to = Config(c => c.Materials = new() { ["Keep"] = "Principled BSDF", ["Bella Skin"] = "Principled BSDF", ["Old Skin"] = "Principled BSDF" });
        to.Engine = "EEVEE";

        var changes = SemanticDiff.Diff(from, to);

        Assert.Contains(changes, c => c.Path == "materials.Bella Skin" && c.Kind == "added");
        Assert.Contains(changes, c => c.Path == "materials.Old Skin" && c.Kind == "modified");
        Assert.Contains(changes, c => c.Path == "engine" && c.From == "CYCLES" && c.To == "EEVEE");
    }

    [Fact]
    public void Diff_masks_node_token_values()
    {
        var changes = SemanticDiff.Diff(Config(), Config(c => c.NodeToken = "rnd_zzzz9999"));
        var token = changes.Single(c => c.Path == "nodeToken");
        Assert.DoesNotContain("zzzz9999", token.To);
        Assert.Contains("••••", token.To);
    }

    [Fact]
    public void Diff_of_equal_configs_is_empty()
    {
        Assert.Empty(SemanticDiff.Diff(Config(), Config()));
    }

    [Fact]
    public void Lock_and_unlock_are_described()
    {
        var locked = SemanticDiff.Diff(Config(), Config(c => c.IsActive = false)).Single(c => c.Path == "isActive");
        Assert.Equal("off", locked.To);

        var unlocked = SemanticDiff.Diff(Config(c => c.IsActive = false), Config()).Single(c => c.Path == "isActive");
        Assert.Equal("on", unlocked.To);
    }

    // ── commit messages ─────────────────────────────────────────────────────

    [Fact]
    public void Message_from_single_sample_change()
    {
        var changes = SemanticDiff.Diff(Config(), Config(c => c.Render.Samples = 256));
        Assert.Equal("Samples 128 → 256", CommitMessage.From(changes));
    }

    [Fact]
    public void Message_for_enabled_output()
    {
        var changes = SemanticDiff.Diff(Config(), Config(c => c.Outputs.Add("turntable")));
        Assert.Equal("Enable turntable output", CommitMessage.From(changes));
    }

    [Fact]
    public void Message_for_engine_switch()
    {
        var changes = SemanticDiff.Diff(Config(), Config(c => c.Engine = "EEVEE"));
        Assert.Equal("Engine CYCLES → EEVEE", CommitMessage.From(changes));
    }

    [Fact]
    public void Message_caps_at_three_parts_plus_more()
    {
        var to = Config(c =>
        {
            c.SourceFile = "//characters/bella/bella_rig_v015.blend";
            c.Description = "changed";
            c.Outputs.Add("turntable");
            c.Outputs.Add("thumbnail");
            c.Render.Samples = 512;
        });
        var message = CommitMessage.From(SemanticDiff.Diff(Config(), to));
        Assert.Contains("+2 more", message);
        Assert.True(message.Length <= 120);
    }

    [Fact]
    public void Identical_configs_never_produce_a_dirty_state()
    {
        var snapshot = System.Text.Json.JsonSerializer.SerializeToNode(Config());
        var canonical = CanonicalJson.Serialize(snapshot);
        Assert.Equal(ContentHash.Of(canonical), ContentHash.Of(CanonicalJson.Serialize(
            System.Text.Json.JsonSerializer.SerializeToNode(Config()))));
    }
}
