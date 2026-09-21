using System.Text.Json.Nodes;
using Xunit;

namespace Weft.Cli.Tests;

/// <summary>
/// Drives the real CLI command implementations against the real API pipeline
/// (in-memory SQLite): login → use → add → edit → commit → log → rollback →
/// renders → rm, asserting exit codes and output along the way.
/// </summary>
public class CliFlowTests : IClassFixture<CliApiFactory>, IDisposable
{
    private readonly CliApiFactory _factory;
    private readonly HttpClient _http;
    private readonly StringWriter _out = new();
    private readonly string _configPath;

    public CliFlowTests(CliApiFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient();
        _configPath = Path.Combine(Path.GetTempPath(), $"bldr-{Guid.NewGuid():N}.json");
        Environment.SetEnvironmentVariable("BLDR_CONFIG", _configPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BLDR_CONFIG", null);
        if (File.Exists(_configPath)) File.Delete(_configPath);
        _out.Dispose();
    }

    private Task<int> Run(params string[] args) => Commands.RunAsync(args, _out, _http);

    /// <summary>Runs a command and returns (exit code, only its own output).</summary>
    private async Task<(int Code, string Out)> RunD(params string[] args)
    {
        var before = _out.ToString().Length;
        var code = await Commands.RunAsync(args, _out, _http);
        return (code, _out.ToString()[before..]);
    }

    private async Task LoginAndUseCoffeeRun()
    {
        Assert.Equal(0, await Run("login", "--email", "admin@example.com", "--password", "admin"));
        Assert.Equal(0, await Run("use", "Coffee Run"));
        _out.ToString(); // drain
    }

    [Fact]
    public async Task Full_vcs_lifecycle_through_the_cli()
    {
        // anonymous use is rejected
        var (code, text) = await RunD("projects");
        Assert.Equal(1, code);
        Assert.Contains("bldr login", text);

        // login → session stored
        (code, text) = await RunD("login", "--email", "admin@example.com", "--password", "admin");
        Assert.Equal(0, code);
        var config = JsonNode.Parse(File.ReadAllText(_configPath))!;
        Assert.True(config["Session"] is not null, "session missing from config file");

        // bad password rejected
        (code, _) = await RunD("logout");
        Assert.Equal(0, code);
        (code, text) = await RunD("login", "--email", "admin@example.com", "--password", "wrong");
        Assert.Equal(1, code);
        Assert.Contains("Invalid email or password", text);
        (code, _) = await RunD("login", "--email", "admin@example.com", "--password", "admin");
        Assert.Equal(0, code);

        // productions list includes all three seeds
        (code, text) = await RunD("projects");
        Assert.Equal(0, code);
        Assert.Contains("Coffee Run", text);
        Assert.Contains("Ironvale", text);

        // set active production
        var (useCode, _) = await RunD("use", "Coffee Run");
        Assert.Equal(0, useCode);

        // add a new asset → v1 initial commit
        (code, text) = await RunD("add", "Cli Test Rig", "--source", "//characters/cli_test.blend",
            "--description", "added by the cli test");
        Assert.Equal(0, code);
        Assert.Contains("under version control", text);
        Assert.Contains("v1", text);
        Assert.Contains("Initial commit", text);

        // status lists the new asset alongside the seeded ones
        (code, text) = await RunD("status");
        Assert.Equal(0, code);
        Assert.Contains("Cli Test Rig", text);
        Assert.Contains("Coffee Run", text);

        // edit the draft
        (code, text) = await RunD("edit", "Cli Test Rig", "--samples", "512", "--enable", "turntable");
        Assert.Equal(0, code);
        Assert.Contains("draft updated", text);

        // diff shows the two changes
        (code, text) = await RunD("diff", "Cli Test Rig");
        Assert.Equal(0, code);
        Assert.Contains("render.samples: 128 → 512", text);
        Assert.Contains("+ outputs: turntable", text);

        // commit with the user's example message
        (code, text) = await RunD("commit", "Cli Test Rig", "-m", "firstrig");
        Assert.Equal(0, code);
        Assert.Contains("firstrig", text);

        // committing again is a no-op
        (code, text) = await RunD("commit", "Cli Test Rig");
        Assert.Equal(0, code);
        Assert.Contains("nothing to commit", text);

        // log shows both commits, newest first
        (code, text) = await RunD("log", "Cli Test Rig");
        Assert.Equal(0, code);
        Assert.True(text.IndexOf("v2 ", StringComparison.Ordinal) < text.IndexOf("v1 ", StringComparison.Ordinal),
            "log should be newest-first: " + text);
        Assert.Contains("firstrig", text);
        Assert.Contains("HEAD", text);

        // rollback restores v1 as a new commit
        (code, text) = await RunD("rollback", "Cli Test Rig", "v1");
        Assert.Equal(0, code);
        Assert.Contains("restored as v3", text);

        // queue a render against the current snapshot
        (code, text) = await RunD("render", "Cli Test Rig", "final", "--data", """{"frame":42}""");
        Assert.Equal(0, code);
        Assert.Contains("final — completed on v3", text);

        // remove the asset entirely
        (code, text) = await RunD("rm", "Cli Test Rig", "--yes");
        Assert.Equal(0, code);
        Assert.Contains("removed", text);
    }

    [Fact]
    public async Task Ambiguous_and_missing_assets_fail_cleanly()
    {
        await LoginAndUseCoffeeRun();

        Assert.Equal(1, await Run("commit", "No Such Asset"));
        Assert.Contains("no asset matching", _out.ToString());

        // 'Harbor' matches exactly one — resolves
        Assert.Equal(0, await Run("log", "Harbor"));
    }

    [Fact]
    public async Task Commit_without_target_commits_the_single_dirty_asset()
    {
        await LoginAndUseCoffeeRun();

        Assert.Equal(0, await Run("edit", "Bella", "--description", "dirty for the no-target commit test"));
        Assert.Equal(0, await Run("commit", "-m", "no target needed"));
        Assert.Contains("Bella — Character Rig: v", _out.ToString());
        Assert.Contains("no target needed", _out.ToString());
    }
}
