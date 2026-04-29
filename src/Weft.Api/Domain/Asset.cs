namespace Weft.Api.Domain;

/// <summary>
/// A Blender asset (character, scene, set piece, material library) under
/// version control. WorkingConfig is the editable draft; the latest Commit is
/// HEAD. "Dirty" (draft differs from HEAD) replaces git's staging area —
/// exactly one draft, always diffing against HEAD.
/// </summary>
public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public AssetConfig WorkingConfig { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public List<Commit> Commits { get; set; } = [];
    public List<RenderJob> RenderJobs { get; set; } = [];
}
