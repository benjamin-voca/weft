using System.Security.Cryptography;
using System.Text;
using Weft.Api.Domain;

namespace Weft.Api.Common;

public sealed record RenderOutcome(
    string Status, int? ExitCode, int DurationMs, int Attempts, string? Error);

/// <summary>
/// Deterministic render-farm simulator. Stands in for the actual Blender
/// dispatch so the platform is fully demoable offline; the same rules drive
/// seeds, the API and tests.
///   • locked asset                → blocked
///   • output not enabled          → blocked
///   • "broken" in the source path → always fails (GPU OOM, retried to the max)
///   • job data containing "force":"fail" → always fails (also a demo affordance)
///   • otherwise                   → ~12% transient GPU OOM, saved by a second attempt
/// </summary>
public static class RenderSimulator
{
    public static RenderOutcome Run(AssetConfig config, string output, string jobDataJson, Guid seedKey)
    {
        if (!config.IsActive)
            return new("blocked", null, 0, 0, "Asset is locked");

        if (!config.Outputs.Contains(output))
            return new("blocked", null, 0, 0, $"Output '{output}' is not enabled for this asset");

        if (!config.SourceFile.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)
            || !(config.SourceFile.StartsWith("//") || config.SourceFile.StartsWith("http")))
            return new("failed", 1, 0, 1, $"Invalid source path: '{config.SourceFile}'");

        if (config.SourceFile.Contains("broken"))
            return new("failed", 1, 600_000, config.Render.Samples >= 256 ? 3 : 2,
                $"GPU out of memory after {Math.Max(2, config.Render.Samples / 128)} attempts");

        if (jobDataJson.Contains("force") && jobDataJson.Contains("fail"))
            return new("failed", 1, 600_000, 1, "GPU out of memory");

        var transientFailure = Roll(seedKey, output, jobDataJson) < 12;
        if (transientFailure)
        {
            if (config.Render.Samples > 1)
                return new("completed", 0, 300_000, 2, null);
            return new("failed", 1, 600_000, 1, "GPU out of memory");
        }

        var duration = 25_000 + Roll(seedKey, jobDataJson, output) * 1_200;
        return new("completed", 0, duration, 1, null);
    }

    private static int Roll(Guid seedKey, string a, string b)
    {
        var input = Encoding.UTF8.GetBytes(seedKey.ToString() + a + b);
        return SHA256.HashData(input)[0];
    }
}
