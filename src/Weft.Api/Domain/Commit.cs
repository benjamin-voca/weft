namespace Weft.Api.Domain;

/// <summary>
/// An immutable snapshot of an asset's config. Never rewritten — rollbacks
/// create a new commit that restores an older snapshot.
/// </summary>
public sealed class Commit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public int Version { get; set; }
    public string Hash { get; set; } = "";
    public string Message { get; set; } = "";
    public string Author { get; set; } = "you";
    public string SnapshotJson { get; set; } = "";
    public List<ChangeEntry> Changes { get; set; } = [];
    public bool IsRollback { get; set; }
    public int? RestoredVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Asset? Asset { get; set; }
}

/// <summary>A single field-level change, produced by the semantic differ.</summary>
public sealed record ChangeEntry(string Path, string Kind, string? From, string? To);
