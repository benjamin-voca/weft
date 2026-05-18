using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Domain;
using Weft.Api.Versioning;

namespace Weft.Api.Data;

public sealed class WeftDb(DbContextOptions<WeftDb> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Commit> Commits => Set<Commit>();
    public DbSet<RenderJob> RenderJobs => Set<RenderJob>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(80).IsRequired();
            e.Property(p => p.Description).HasMaxLength(300);
        });

        b.Entity<Asset>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(80).IsRequired();
            e.Property(a => a.WorkingConfig)
                .HasConversion(
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<AssetConfig>(s, (JsonSerializerOptions?)null) ?? new AssetConfig())
                .HasColumnType("jsonb");
            e.HasIndex(a => a.ProjectId);
        });

        b.Entity<Commit>(e =>
        {
            e.Property(c => c.Hash).HasMaxLength(8);
            e.Property(c => c.Message).HasMaxLength(160);
            e.Property(c => c.SnapshotJson).HasColumnType("jsonb");
            e.Property(c => c.Changes)
                .HasConversion(
                    list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<List<ChangeEntry>>(s, (JsonSerializerOptions?)null) ?? new List<ChangeEntry>())
                .HasColumnType("jsonb");
            e.HasIndex(c => new { c.AssetId, c.Version }).IsUnique();
            e.HasOne(c => c.Asset)
                .WithMany(a => a.Commits)
                .HasForeignKey(c => c.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RenderJob>(e =>
        {
            e.Property(r => r.Status).HasMaxLength(16);
            e.Property(r => r.JobDataJson).HasColumnType("jsonb");
            e.Property(r => r.Error).HasMaxLength(300);
            e.HasIndex(r => new { r.AssetId, r.CreatedAt });
            e.HasOne(r => r.Asset)
                .WithMany(a => a.RenderJobs)
                .HasForeignKey(r => r.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(80);
            e.Property(u => u.Role).HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
        });
    }

    /// <summary>HEAD snapshot for an asset, or null before the first commit.</summary>
    public async Task<Commit?> HeadAsync(Guid assetId) =>
        await Commits.Where(c => c.AssetId == assetId)
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync();

    /// <summary>True when the working draft differs from HEAD (Weft's "dirty" check).</summary>
    public static bool IsDirty(Asset asset, Commit? head)
    {
        if (head is null) return true;
        // jsonb normalizes key order in storage, so both sides must be
        // re-canonicalized before comparing.
        var working = CanonicalJson.Serialize(JsonSerializer.SerializeToNode(asset.WorkingConfig));
        var headSnapshot = CanonicalJson.Serialize(JsonNode.Parse(head.SnapshotJson));
        return working != headSnapshot;
    }
}
