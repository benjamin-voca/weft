using Weft.Api.Domain;

namespace Weft.Api.Versioning;

/// <summary>
/// Generates a human commit message from the semantic diff, so routine edits
/// don't need hand-written messages (override at commit time if desired).
/// </summary>
public static class CommitMessage
{
    private const int MaxParts = 3;

    public static string From(List<ChangeEntry> changes)
    {
        if (changes.Count == 0) return "No changes";

        var parts = changes.Select(Describe).Take(MaxParts).ToList();
        if (changes.Count > MaxParts) parts.Add($"+{changes.Count - MaxParts} more");

        var message = string.Join(" · ", parts);
        return message.Length > 120 ? message[..117] + "…" : message;
    }

    private static string Describe(ChangeEntry c) => c.Path switch
    {
        "sourceFile" => $"Switch source to {FileOf(c.To ?? c.From ?? "")}",
        "description" => "Update description",
        "blenderVersion" => $"Blender {c.From} → {c.To}",
        "engine" => $"Engine {c.From} → {c.To}",
        "resX" or "resY" => "Change resolution",
        "nodeToken" => "Rotate render-node token",
        "outputPattern" => "Update output naming",
        "isActive" => c.To == "on" ? "Unlock asset" : "Lock asset",
        "outputs" => c.Kind switch
        {
            "added" => $"Enable {c.To} output",
            "removed" => $"Disable {c.From} output",
            _ => "Update outputs"
        },
        "render.samples" => $"Samples {c.From} → {c.To}",
        "render.denoise" => c.To == "on" ? "Enable denoising" : "Disable denoising",
        _ => c.Path.StartsWith("materials.") switch
        {
            true => c.Kind switch
            {
                "added" => $"Add material {c.Path["materials.".Length..]}",
                "removed" => $"Remove material {c.Path["materials.".Length..]}",
                _ => $"Update material {c.Path["materials.".Length..]}"
            },
            false => $"Update {c.Path}"
        }
    };

    private static string FileOf(string path)
    {
        var name = path.Split('/').LastOrDefault() ?? path;
        return name.Length > 40 ? name[..40] : name;
    }
}
