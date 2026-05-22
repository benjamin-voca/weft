using Weft.Api.Domain;
using System.Text.Json.Nodes;

namespace Weft.Api.Features;

// ── Projects ────────────────────────────────────────────────────────────────
public sealed record CreateProjectRequest(string Name, string? Description);
public sealed record ProjectSummary(Guid Id, string Name, string Description, int AssetCount, DateTime CreatedAt);

// ── Assets ──────────────────────────────────────────────────────────────────
public sealed record CreateAssetRequest(string Name, AssetConfig Config);
public sealed record UpdateConfigRequest(AssetConfig Config);
public sealed record CommitRequest(string? Message);
public sealed record RollbackRequest(int Version, string? Message);

public sealed record AssetSummary(
    Guid Id, string Name, string SourceFile, List<string> Outputs, bool IsActive, int Version, string Hash, bool Dirty,
    int RenderCount, double SuccessRate, DateTime? LastRenderAt, DateTime UpdatedAt);

public sealed record AssetDetail(
    Guid Id, Guid ProjectId, string ProjectName, string Name, AssetConfig WorkingConfig,
    int HeadVersion, string HeadHash, bool Dirty, List<ChangeEntry> DraftDiff,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CommitDto(
    Guid Id, int Version, string Hash, string Message, string Author, List<ChangeEntry> Changes,
    bool IsRollback, int? RestoredVersion, DateTime CreatedAt);

public sealed record DiffEndpointDto(int Version, string Hash, string Label);
public sealed record DiffDto(DiffEndpointDto From, DiffEndpointDto To, List<ChangeEntry> Changes);

// ── Render jobs ─────────────────────────────────────────────────────────────
public sealed record QueueRenderRequest(string Output, JsonNode? JobData);
public sealed record RenderDto(
    Guid Id, Guid AssetId, string AssetName, int AssetVersion, string Output, JsonNode JobData,
    string Status, int? ExitCode, int DurationMs, int Attempts, string? Error,
    bool IsReplay, Guid? ReplayOfId, DateTime CreatedAt);

// ── Stats ───────────────────────────────────────────────────────────────────
public sealed record Totals(int Assets, int Active, int Renders, int Completed, int Failed, int Blocked, double SuccessRate);
public sealed record LatencyStats(double AvgMs, int P95Ms);
public sealed record SeriesPoint(string Date, int Completed, int Failed, int Blocked);
public sealed record AssetHealth(
    Guid Id, string Name, bool IsActive, int Version, bool Dirty,
    int Renders, double SuccessRate, double AvgMs, DateTime? LastRenderAt);
public sealed record ProjectStats(Totals Totals, LatencyStats Latency, List<SeriesPoint> Series,
    List<AssetHealth> Assets, List<RenderDto> Recent);
