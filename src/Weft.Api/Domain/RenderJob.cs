namespace Weft.Api.Domain;

/// <summary>
/// A render/export job executed against a Blender asset. Every job pins the
/// config version it used, so failed renders can be blamed on the change that
/// introduced them.
/// </summary>
public sealed class RenderJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public int AssetVersion { get; set; }
    public string Output { get; set; } = "";
    public string JobDataJson { get; set; } = "{}";
    /// <summary>completed | failed | blocked</summary>
    public string Status { get; set; } = "completed";
    public int? ExitCode { get; set; }
    public int DurationMs { get; set; }
    public int Attempts { get; set; } = 1;
    public string? Error { get; set; }
    public bool IsReplay { get; set; }
    public Guid? ReplayOfId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Asset? Asset { get; set; }
}
