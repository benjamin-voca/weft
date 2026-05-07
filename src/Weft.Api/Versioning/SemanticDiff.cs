using Weft.Api.Domain;

namespace Weft.Api.Versioning;

/// <summary>
/// Field-level semantic differ for AssetConfig. Produces ordered,
/// human-readable change entries instead of a raw text diff — every change
/// names the config path it touched and the before/after values.
/// </summary>
public static class SemanticDiff
{
    public static List<ChangeEntry> Diff(AssetConfig from, AssetConfig to)
    {
        var changes = new List<ChangeEntry>();

        if (from.SourceFile != to.SourceFile)
            changes.Add(new("sourceFile", "modified", from.SourceFile, to.SourceFile));

        if (from.Description != to.Description)
            changes.Add(new("description", "modified", from.Description, to.Description));

        if (from.BlenderVersion != to.BlenderVersion)
            changes.Add(new("blenderVersion", "modified", from.BlenderVersion, to.BlenderVersion));

        if (from.Engine != to.Engine)
            changes.Add(new("engine", "modified", from.Engine, to.Engine));

        if (from.ResX != to.ResX)
            changes.Add(new("resX", "modified", from.ResX.ToString(), to.ResX.ToString()));

        if (from.ResY != to.ResY)
            changes.Add(new("resY", "modified", from.ResY.ToString(), to.ResY.ToString()));

        if (from.Render.Samples != to.Render.Samples)
            changes.Add(new("render.samples", "modified",
                from.Render.Samples.ToString(), to.Render.Samples.ToString()));

        if (from.Render.Denoise != to.Render.Denoise)
            changes.Add(new("render.denoise", "modified",
                Display(from.Render.Denoise), Display(to.Render.Denoise)));

        if (from.NodeToken != to.NodeToken)
            changes.Add(new("nodeToken", "modified", Mask(from.NodeToken), Mask(to.NodeToken)));

        if (from.OutputPattern != to.OutputPattern)
            changes.Add(new("outputPattern", "modified", from.OutputPattern, to.OutputPattern));

        if (from.IsActive != to.IsActive)
            changes.Add(new("isActive", "modified", Display(from.IsActive), Display(to.IsActive)));

        foreach (var output in to.Outputs.Except(from.Outputs))
            changes.Add(new("outputs", "added", null, output));
        foreach (var output in from.Outputs.Except(to.Outputs))
            changes.Add(new("outputs", "removed", output, null));

        foreach (var (name, shader) in to.Materials)
        {
            if (!from.Materials.TryGetValue(name, out var old))
                changes.Add(new($"materials.{name}", "added", null, shader));
            else if (old != shader)
                changes.Add(new($"materials.{name}", "modified", old, shader));
        }
        foreach (var (name, shader) in from.Materials.Where(m => !to.Materials.ContainsKey(m.Key)))
            changes.Add(new($"materials.{name}", "removed", shader, null));

        return changes
            .OrderBy(c => c.Kind switch { "modified" => 0, "added" => 1, _ => 2 })
            .ThenBy(c => c.Path, StringComparer.Ordinal)
            .ToList();
    }

    public static string Mask(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return "";
        var tail = secret.Length <= 4 ? secret : secret[^4..];
        return $"••••{tail}";
    }

    private static string Display(bool value) => value ? "on" : "off";
}
