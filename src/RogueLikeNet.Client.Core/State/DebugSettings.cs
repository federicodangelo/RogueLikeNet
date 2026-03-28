namespace RogueLikeNet.Client.Core.State;

/// <summary>
/// Debug mode settings for offline gameplay. When debug mode is enabled,
/// various gameplay toggles can be controlled via keyboard shortcuts.
/// </summary>
public sealed class DebugSettings
{
    /// <summary>Whether debug mode is active (only available offline).</summary>
    public bool Enabled { get; set; }

    /// <summary>When true, FOV/visibility calculations are skipped — player sees everything.</summary>
    public bool VisibilityOff { get; set; } = true;

    /// <summary>When true, player ignores tile collisions and can walk anywhere.</summary>
    public bool CollisionsOff { get; set; } = true;

    /// <summary>When true, the player cannot take damage.</summary>
    public bool Invulnerable { get; set; } = true;

    /// <summary>When true, light calculations are skipped — everything rendered at full brightness.</summary>
    public bool LightOff { get; set; } = true;

    /// <summary>When true, movement has no delay and holding a direction moves 4 tiles at a time.</summary>
    public bool MaxSpeed { get; set; } = true;

    /// <summary>Zoom level offset. 0 = default, negative = zoom out, positive = zoom in.</summary>
    public int ZoomLevel { get; set; }

    /// <summary>Effective tile width considering zoom. Used by renderers.</summary>
    public int EffectiveTileWidth => ComputeEffectiveTileSize(Rendering.AsciiDraw.TileWidth);

    /// <summary>Effective tile height considering zoom. Used by renderers.</summary>
    public int EffectiveTileHeight => ComputeEffectiveTileSize(Rendering.AsciiDraw.TileHeight);

    /// <summary>Effective font scale considering zoom. Used by renderers.</summary>
    public float EffectiveFontScale => ComputeEffectiveSize(Rendering.AsciiDraw.FontScale);

    /// <summary>Computed chunk range for the server based on zoom level.</summary>
    public int ChunkRange => Math.Max(1, (int)MathF.Ceiling(1.0f / ComputeEffectiveSize(1.0f)));

    public void Reset()
    {
        VisibilityOff = true;
        CollisionsOff = true;
        LightOff = true;
        Invulnerable = true;
        MaxSpeed = true;
        ZoomLevel = 0;
    }

    private int ComputeEffectiveTileSize(int size)
    {
        return Math.Max(2, (int)ComputeEffectiveSize(size));
    }

    private float ComputeEffectiveSize(float size)
    {
        if (!Enabled || ZoomLevel == 0) return size;
        float scale = MathF.Pow(1.25f, -ZoomLevel);
        return size * scale;
    }
}
