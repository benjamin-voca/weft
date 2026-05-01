namespace Weft.Api.Domain;

/// <summary>
/// The full, versioned state of a Blender asset: its source file, render
/// settings, materials and outputs. Every commit stores a complete snapshot —
/// history is snapshot-based, which makes diffs, rollbacks and audits lossless.
/// </summary>
public sealed class AssetConfig
{
    public string SourceFile { get; set; } = "";
    public string Description { get; set; } = "";
    public string BlenderVersion { get; set; } = "4.2.3";
    public string Engine { get; set; } = "CYCLES";
    public int ResX { get; set; } = 1920;
    public int ResY { get; set; } = 1080;
    public RenderSettings Render { get; set; } = new();
    public List<string> Outputs { get; set; } = [];
    public Dictionary<string, string> Materials { get; set; } = new();
    public string NodeToken { get; set; } = "";
    public string OutputPattern { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>Per-asset render quality knobs.</summary>
public sealed class RenderSettings
{
    public int Samples { get; set; } = 128;
    public bool Denoise { get; set; } = true;
}
