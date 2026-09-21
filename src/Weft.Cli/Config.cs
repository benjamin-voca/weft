using System.Text.Json;

namespace Weft.Cli;

/// <summary>
/// Local CLI state (~/.bldr/config.json, overridable with BLDR_CONFIG):
/// server URL, session cookie and the active production.
/// </summary>
public sealed class BldrConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5015";
    public string? Session { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public static string Path =>
        Environment.GetEnvironmentVariable("BLDR_CONFIG")
        ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bldr", "config.json");

    public static BldrConfig Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<BldrConfig>(File.ReadAllText(Path)) ?? new BldrConfig();
        }
        catch { /* corrupt config — start fresh */ }
        return new BldrConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ForgetSession()
    {
        Session = null;
        Save();
    }
}
