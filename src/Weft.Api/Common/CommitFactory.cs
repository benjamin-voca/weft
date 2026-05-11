using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Data;
using Weft.Api.Domain;
using Weft.Api.Versioning;

namespace Weft.Api.Common;

/// <summary>Single place where commits are minted so canonicalization is identical everywhere.</summary>
public static class CommitFactory
{
    public static Commit Create(
        Guid assetId, int version, AssetConfig config, List<ChangeEntry> changes,
        string message, string author, string? customMessage,
        bool isRollback = false, int? restoredVersion = null, DateTime? createdAt = null)
    {
        var snapshot = CanonicalJson.Serialize(JsonSerializer.SerializeToNode(config));
        return new Commit
        {
            AssetId = assetId,
            Version = version,
            Hash = ContentHash.Of(snapshot),
            SnapshotJson = snapshot,
            Changes = changes,
            Message = Sanitize(customMessage) ?? message,
            Author = author,
            IsRollback = isRollback,
            RestoredVersion = restoredVersion,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    public static AssetConfig ParseSnapshot(string snapshotJson) =>
        JsonSerializer.Deserialize<AssetConfig>(snapshotJson) ?? new AssetConfig();

    public static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        message = message.Trim();
        return message.Length > 120 ? message[..120] : message;
    }

    /// <summary>Config validation shared by create and update.</summary>
    public static string? Validate(AssetConfig config)
    {
        var source = config.SourceFile ?? "";
        if (!(source.StartsWith("//") || source.StartsWith("http")) || !source.EndsWith(".blend"))
            return "Source file must be a .blend path (//pool/path/file.blend)";
        if (config.Render.Samples is < 1 or > 4096)
            return "Samples must be between 1 and 4096";
        if (config.ResX is < 16 or > 8192 || config.ResY is < 16 or > 8192)
            return "Resolution must be between 16 and 8192 pixels";
        return null;
    }

    public static async Task<Commit?> HeadAsync(WeftDb db, Guid assetId) =>
        await db.Commits.Where(c => c.AssetId == assetId)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync();
}
