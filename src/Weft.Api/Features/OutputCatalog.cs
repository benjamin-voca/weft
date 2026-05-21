namespace Weft.Api.Features;

/// <summary>Static catalog powering the render-output composer in the UI.</summary>
public static class OutputCatalog
{
    public static readonly (string Group, string[] Outputs)[] Groups =
    [
        ("renders", ["final", "animation"]),
        ("previews", ["viewport", "thumbnail", "turntable"]),
        ("data", ["bake", "export-usd", "export-abc"]),
    ];
}
