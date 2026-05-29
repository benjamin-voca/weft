using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Weft.Api.Common;
using Weft.Api.Domain;
using Weft.Api.Data;
using Weft.Api.Features;

namespace Weft.Api.Features;

public static class RenderEndpoints
{
    public static void MapRenders(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Renders").RequireAuthorization();

        // ── render log for an asset ─────────────────────────────────────────
        group.MapGet("/assets/{id:guid}/renders", async (Guid id, int? limit, WeftDb db) =>
        {
            if (!await db.Assets.AnyAsync(a => a.Id == id))
                return Results.NotFound(new { error = "Asset not found" });

            var take = Math.Clamp(limit ?? 60, 1, 200);
            var renders = await db.RenderJobs.Where(r => r.AssetId == id)
                .OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync();
            var name = (await db.Assets.FindAsync(id))!.Name;
            return Results.Ok(renders.Select(r => ToDto(r, name)).ToList());
        });

        // ── queue a render against the CURRENT config ───────────────────────
        group.MapPost("/assets/{id:guid}/renders", async (Guid id, QueueRenderRequest req, WeftDb db) =>
        {
            var asset = await db.Assets.FindAsync(id);
            if (asset is null) return Results.NotFound(new { error = "Asset not found" });
            var head = await CommitFactory.HeadAsync(db, id);
            if (head is null) return Results.Conflict(new { error = "Asset has no commits yet" });

            var output = (req.Output ?? "").Trim();
            if (output.Length == 0) return Results.BadRequest(new { error = "Output is required" });

            var jobData = req.JobData ?? new JsonObject();
            var outcome = RenderSimulator.Run(asset.WorkingConfig, output, jobData.ToJsonString(), asset.Id);

            var render = new Domain.RenderJob
            {
                AssetId = asset.Id,
                AssetVersion = head.Version,
                Output = output,
                JobDataJson = jobData.ToJsonString(),
                Status = outcome.Status,
                ExitCode = outcome.ExitCode,
                DurationMs = outcome.DurationMs,
                Attempts = outcome.Attempts,
                Error = outcome.Error
            };
            db.RenderJobs.Add(render);
            await db.SaveChangesAsync();
            return Results.Created($"/api/renders/{render.Id}", ToDto(render, asset.Name));
        });

        // ── replay a past render against the CURRENT config ──────────────────
        group.MapPost("/renders/{id:guid}/replay", async (Guid id, WeftDb db) =>
        {
            var original = await db.RenderJobs.FindAsync(id);
            if (original is null) return Results.NotFound(new { error = "Render job not found" });

            var asset = await db.Assets.FindAsync(original.AssetId);
            if (asset is null) return Results.Conflict(new { error = "Asset no longer exists" });
            var head = await CommitFactory.HeadAsync(db, asset.Id);

            var outcome = RenderSimulator.Run(asset.WorkingConfig, original.Output, original.JobDataJson, asset.Id);
            var replay = new Domain.RenderJob
            {
                AssetId = asset.Id,
                AssetVersion = head?.Version ?? 0,
                Output = original.Output,
                JobDataJson = original.JobDataJson,
                Status = outcome.Status,
                ExitCode = outcome.ExitCode,
                DurationMs = outcome.DurationMs,
                Attempts = outcome.Attempts,
                Error = outcome.Error,
                IsReplay = true,
                ReplayOfId = original.Id
            };
            db.RenderJobs.Add(replay);
            await db.SaveChangesAsync();
            return Results.Created($"/api/renders/{replay.Id}", ToDto(replay, asset.Name));
        });
    }

    internal static RenderDto ToDto(RenderJob r, string assetName) => new(
        r.Id, r.AssetId, assetName, r.AssetVersion, r.Output,
        JsonNode.Parse(r.JobDataJson) ?? new System.Text.Json.Nodes.JsonObject(),
        r.Status, r.ExitCode, r.DurationMs, r.Attempts, r.Error, r.IsReplay, r.ReplayOfId, r.CreatedAt);
}
