using Microsoft.EntityFrameworkCore;
using Weft.Api.Data;
using Weft.Api.Features;

namespace Weft.Api.Features;

public static class StatsEndpoints
{
    public static void MapStats(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{id:guid}/stats", async (Guid id, WeftDb db) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == id))
                return Results.NotFound(new { error = "Project not found" });

            var assets = await db.Assets.Where(a => a.ProjectId == id).OrderBy(a => a.Name).ToListAsync();
            var assetIds = assets.Select(a => a.Id).ToList();
            var heads = assetIds.Count == 0
                ? new Dictionary<Guid, Domain.Commit>()
                : await AssetsEndpoints.HeadsByAsset(db, assetIds);
            var renders = assetIds.Count == 0
                ? []
                : await db.RenderJobs.Where(r => assetIds.Contains(r.AssetId))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

            var completed = renders.Count(r => r.Status == "completed");
            var failed = renders.Count(r => r.Status == "failed");
            var blocked = renders.Count(r => r.Status == "blocked");
            var successRate = renders.Count == 0
                ? 100.0
                : Math.Round(100.0 * completed / renders.Count, 1);

            var timed = renders.Where(r => r.DurationMs > 0).Select(r => r.DurationMs).OrderBy(x => x).ToList();
            var latency = new LatencyStats(
                timed.Count == 0 ? 0 : Math.Round(timed.Average(), 0),
                timed.Count == 0 ? 0 : timed[(int)Math.Min(timed.Count - 1, Math.Floor(timed.Count * 0.95))]);

            var today = DateTime.UtcNow.Date;
            var series = Enumerable.Range(0, 14).Select(i =>
            {
                var day = today.AddDays(-13 + i);
                var next = day.AddDays(1);
                var dayRenders = renders.Where(r => r.CreatedAt >= day && r.CreatedAt < next).ToList();
                return new SeriesPoint(
                    day.ToString("yyyy-MM-dd"),
                    dayRenders.Count(r => r.Status == "completed"),
                    dayRenders.Count(r => r.Status == "failed"),
                    dayRenders.Count(r => r.Status == "blocked"));
            }).ToList();

            var assetHealth = assets.Select(a =>
            {
                var list = renders.Where(r => r.AssetId == a.Id).ToList();
                var ok = list.Count(r => r.Status == "completed");
                var last = list.MaxBy(r => r.CreatedAt)?.CreatedAt;
                return new AssetHealth(
                    a.Id, a.Name, a.WorkingConfig.IsActive,
                    heads.GetValueOrDefault(a.Id)?.Version ?? 0,
                    WeftDb.IsDirty(a, heads.GetValueOrDefault(a.Id)),
                    list.Count,
                    list.Count == 0 ? 100.0 : Math.Round(100.0 * ok / list.Count, 1),
                    list.Count == 0 ? 0 : Math.Round(list.Average(r => (double)r.DurationMs), 0),
                    last);
            }).ToList();

            var recent = renders.Take(10)
                .Select(r => RenderEndpoints.ToDto(r, assets.FirstOrDefault(a => a.Id == r.AssetId)?.Name ?? ""))
                .ToList();

            return Results.Ok(new ProjectStats(
                new Totals(assets.Count, assets.Count(a => a.WorkingConfig.IsActive),
                    renders.Count, completed, failed, blocked, successRate),
                latency, series, assetHealth, recent));
        }).RequireAuthorization().WithTags("Stats");
    }
}
