using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Common;
using Weft.Api.Data;
using Weft.Api.Domain;
using Weft.Api.Features;
using Weft.Api.Versioning;

namespace Weft.Api.Features;

public static class AssetsEndpoints
{
    public static void MapAssets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Assets").RequireAuthorization();

        // ── list assets in a project ────────────────────────────────────────
        group.MapGet("/projects/{projectId:guid}/assets", async (Guid projectId, WeftDb db) =>
        {
            var assets = await db.Assets.Where(a => a.ProjectId == projectId)
                .OrderBy(a => a.Name).ToListAsync();
            var assetIds = assets.Select(a => a.Id).ToList();
            if (assetIds.Count == 0) return Results.Ok(Array.Empty<AssetSummary>());

            var heads = await HeadsByAsset(db, assetIds);
            var agg = await db.RenderJobs.Where(r => assetIds.Contains(r.AssetId))
                .GroupBy(r => r.AssetId)
                .Select(g => new
                {
                    AssetId = g.Key,
                    Count = g.Count(),
                    Completed = g.Count(r => r.Status == "completed"),
                    Last = g.Max(r => r.CreatedAt)
                })
                .ToDictionaryAsync(x => x.AssetId);

            var summaries = assets.Select(a =>
            {
                var head = heads.GetValueOrDefault(a.Id);
                var stats = agg.GetValueOrDefault(a.Id);
                return new AssetSummary(
                    a.Id, a.Name,
                    a.WorkingConfig.SourceFile,
                    a.WorkingConfig.Outputs,
                    a.WorkingConfig.IsActive,
                    head?.Version ?? 0,
                    head?.Hash ?? "",
                    WeftDb.IsDirty(a, head),
                    stats?.Count ?? 0,
                    stats is { Count: > 0 } ? Math.Round(100.0 * stats.Completed / stats.Count, 1) : 100.0,
                    stats?.Last,
                    a.UpdatedAt);
            }).ToList();

            return Results.Ok(summaries);
        });

        // ── create asset (+ v1 commit) ──────────────────────────────────────
        group.MapPost("/projects/{projectId:guid}/assets", async (Guid projectId, CreateAssetRequest req, WeftDb db) =>
        {
            var project = await db.Projects.FindAsync(projectId);
            if (project is null) return Results.NotFound(new { error = "Project not found" });

            var name = (req.Name ?? "").Trim();
            if (name.Length is 0 or > 80)
                return Results.BadRequest(new { error = "Asset name is required (max 80 chars)" });
            if (req.Config is null) return Results.BadRequest(new { error = "Config is required" });
            if (CommitFactory.Validate(req.Config) is { } err) return Results.BadRequest(new { error = err });

            var asset = new Asset { ProjectId = projectId, Name = name, WorkingConfig = Clone(req.Config) };
            var changes = SemanticDiff.Diff(new AssetConfig(), asset.WorkingConfig);
            var commit = CommitFactory.Create(asset.Id, 1, asset.WorkingConfig, changes,
                "Initial commit", "you", null);

            // Explicit Add: nav-collection discovery would attach the preset-Guid
            // commit as "existing" and emit UPDATE instead of INSERT.
            db.Assets.Add(asset);
            db.Commits.Add(commit);
            await db.SaveChangesAsync();
            return Results.Created($"/api/assets/{asset.Id}", ToDto(asset.Id, project.Name, name, asset, commit, []));
        });

        // ── asset detail (draft diff included) ──────────────────────────────
        group.MapGet("/assets/{id:guid}", async (Guid id, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            var head = await CommitFactory.HeadAsync(db, id);
            var project = await db.Projects.FindAsync(asset.ProjectId);

            var headConfig = head is null ? new AssetConfig() : CommitFactory.ParseSnapshot(head.SnapshotJson);
            return Results.Ok(ToDto(id, project?.Name ?? "", asset.Name, asset, head,
                SemanticDiff.Diff(headConfig, asset.WorkingConfig)));
        });

        // ── update working draft ────────────────────────────────────────────
        group.MapPut("/assets/{id:guid}/config", async (Guid id, UpdateConfigRequest req, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            if (req.Config is null) return Results.BadRequest(new { error = "Config is required" });
            if (CommitFactory.Validate(req.Config) is { } err) return Results.BadRequest(new { error = err });

            asset.WorkingConfig = Clone(req.Config);
            asset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var head = await CommitFactory.HeadAsync(db, id);
            var headConfig = head is null ? new AssetConfig() : CommitFactory.ParseSnapshot(head.SnapshotJson);
            var project = await db.Projects.FindAsync(asset.ProjectId);
            return Results.Ok(ToDto(id, project?.Name ?? "", asset.Name, asset, head,
                SemanticDiff.Diff(headConfig, asset.WorkingConfig)));
        });

        // ── discard draft → restore HEAD into the draft ─────────────────────
        group.MapPost("/assets/{id:guid}/discard", async (Guid id, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            var head = await CommitFactory.HeadAsync(db, id);

            asset.WorkingConfig = head is null ? new AssetConfig() : CommitFactory.ParseSnapshot(head.SnapshotJson);
            asset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var project = await db.Projects.FindAsync(asset.ProjectId);
            return Results.Ok(ToDto(id, project?.Name ?? "", asset.Name, asset, head, []));
        });

        // ── commit draft ────────────────────────────────────────────────────
        group.MapPost("/assets/{id:guid}/commit", async (Guid id, CommitRequest? req, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });

            var head = await CommitFactory.HeadAsync(db, id);
            if (!WeftDb.IsDirty(asset, head))
                return Results.Conflict(new
                {
                    error = $"Nothing to commit — draft matches v{head!.Version} ({head.Hash})"
                });

            var headConfig = head is null ? new AssetConfig() : CommitFactory.ParseSnapshot(head.SnapshotJson);
            var changes = SemanticDiff.Diff(headConfig, asset.WorkingConfig);
            var commit = CommitFactory.Create(asset.Id, (head?.Version ?? 0) + 1, asset.WorkingConfig,
                changes, CommitMessage.From(changes), "you", req?.Message);
            db.Commits.Add(commit);
            asset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Created($"/api/assets/{id}", ToCommitDto(commit));
        });

        // ── history ─────────────────────────────────────────────────────────
        group.MapGet("/assets/{id:guid}/commits", async (Guid id, WeftDb db) =>
        {
            if (!await db.Assets.AnyAsync(a => a.Id == id))
                return Results.NotFound(new { error = "Asset not found" });
            var commits = await db.Commits.Where(c => c.AssetId == id)
                .OrderByDescending(c => c.Version).ToListAsync();
            return Results.Ok(commits.Select(ToCommitDto).ToList());
        });

        // ── diff any two points (defaults: HEAD ↔ draft) ─────────────────────
        group.MapGet("/assets/{id:guid}/diff", async (Guid id, int? from, int? to, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            var head = await CommitFactory.HeadAsync(db, id);

            var fromCommit = from is not null
                ? await db.Commits.SingleOrDefaultAsync(c => c.AssetId == id && c.Version == from)
                : head;
            if (from is not null && fromCommit is null)
                return Results.NotFound(new { error = $"Version v{from} not found" });
            if (to is not null)
            {
                var toCommit = await db.Commits.SingleOrDefaultAsync(c => c.AssetId == id && c.Version == to);
                if (toCommit is null) return Results.NotFound(new { error = $"Version v{to} not found" });

                var a = CommitFactory.ParseSnapshot(fromCommit!.SnapshotJson);
                var b = CommitFactory.ParseSnapshot(toCommit.SnapshotJson);
                return Results.Ok(new DiffDto(
                    new DiffEndpointDto(fromCommit.Version, fromCommit.Hash, $"v{fromCommit.Version}"),
                    new DiffEndpointDto(toCommit.Version, toCommit.Hash, $"v{toCommit.Version}"),
                    SemanticDiff.Diff(a, b)));
            }

            var headConfig = fromCommit is null ? new AssetConfig() : CommitFactory.ParseSnapshot(fromCommit.SnapshotJson);
            var draftHash = ContentHash.Of(CanonicalJson.Serialize(JsonSerializer.SerializeToNode(asset.WorkingConfig)));
            return Results.Ok(new DiffDto(
                new DiffEndpointDto(fromCommit?.Version ?? 0, fromCommit?.Hash ?? "", "HEAD"),
                new DiffEndpointDto(0, draftHash, "Draft"),
                SemanticDiff.Diff(headConfig, asset.WorkingConfig)));
        });

        // ── rollback: restore an old snapshot as a NEW commit ────────────────
        group.MapPost("/assets/{id:guid}/rollback", async (Guid id, RollbackRequest req, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });

            var target = await db.Commits.SingleOrDefaultAsync(c => c.AssetId == id && c.Version == req.Version);
            if (target is null) return Results.NotFound(new { error = $"Version v{req.Version} not found" });

            var head = await CommitFactory.HeadAsync(db, id);
            if (head!.Version == req.Version && !WeftDb.IsDirty(asset, head))
                return Results.Conflict(new { error = $"Already at v{req.Version}" });

            asset.WorkingConfig = CommitFactory.ParseSnapshot(target.SnapshotJson);
            var headConfig = CommitFactory.ParseSnapshot(head.SnapshotJson);
            var changes = SemanticDiff.Diff(headConfig, asset.WorkingConfig);
            var commit = CommitFactory.Create(asset.Id, head.Version + 1, asset.WorkingConfig, changes,
                $"Revert to v{req.Version}", "you", req.Message,
                isRollback: true, restoredVersion: req.Version);
            db.Commits.Add(commit);
            asset.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(ToCommitDto(commit));
        });

        // ── delete asset ────────────────────────────────────────────────────
        group.MapDelete("/assets/{id:guid}", async (Guid id, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            db.Assets.Remove(asset);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    internal static async Task<Dictionary<Guid, Commit>> HeadsByAsset(WeftDb db, List<Guid> assetIds) =>
        (await db.Commits.Where(c => assetIds.Contains(c.AssetId)).ToListAsync())
        .GroupBy(c => c.AssetId)
        .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Version).Last());

    private static AssetConfig Clone(AssetConfig config) =>
        JsonSerializer.Deserialize<AssetConfig>(JsonSerializer.Serialize(config))!;

    internal static CommitDto ToCommitDto(Commit c) =>
        new(c.Id, c.Version, c.Hash, c.Message, c.Author, c.Changes, c.IsRollback, c.RestoredVersion, c.CreatedAt);

    internal static AssetDetail ToDto(Guid id, string projectName, string name, Asset asset, Commit? head,
        List<ChangeEntry> draftDiff) =>
        new(id, asset.ProjectId, projectName, name, asset.WorkingConfig,
            head?.Version ?? 0, head?.Hash ?? "", WeftDb.IsDirty(asset, head), draftDiff,
            asset.CreatedAt, asset.UpdatedAt);
}
