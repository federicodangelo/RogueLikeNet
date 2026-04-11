using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Registry of all tile definitions. Provides fast lookups by numeric ID and
/// helper methods for walkability/transparency checks on the hot path.
/// </summary>
public sealed class TilesRegistry : BaseRegistry<TileDefinition>
{
    // Fast lookup caches indexed by NumericId → property, populated in PostRegister
    private readonly Dictionary<int, bool> _walkableCache = [];
    private readonly Dictionary<int, bool> _transparentCache = [];
    private readonly Dictionary<int, int> _glyphCache = [];
    private readonly Dictionary<int, int> _fgColorCache = [];
    private readonly Dictionary<int, int> _bgColorCache = [];

    protected override void ExtraRegister(TileDefinition def)
    {
        _walkableCache[def.NumericId] = def.Walkable;
        _transparentCache[def.NumericId] = def.Transparent;
        _glyphCache[def.NumericId] = def.GlyphId;
        _fgColorCache[def.NumericId] = def.FgColor;
        _bgColorCache[def.NumericId] = def.BgColor;
    }

    /// <summary>Returns true if the tile type is walkable (without considering placeables).</summary>
    public bool IsWalkable(int tileId)
    {
        return _walkableCache.TryGetValue(tileId, out var walkable) && walkable;
    }

    /// <summary>Returns true if the tile type is transparent (without considering placeables).</summary>
    public bool IsTransparent(int tileId)
    {
        return !_transparentCache.TryGetValue(tileId, out var transparent) || transparent;
    }

    /// <summary>Returns the TileType category for a tile (Floor, Blocked, Water, etc).</summary>
    public TileType GetTileType(int tileId)
    {
        var def = Get(tileId);
        return def?.Type ?? TileType.Void;
    }

    /// <summary>Returns the glyph (CP437 index) for a tile.</summary>
    public int GetGlyphId(int tileId) => _glyphCache.GetValueOrDefault(tileId);

    /// <summary>Returns the foreground color (packed 0xRRGGBB) for a tile.</summary>
    public int GetFgColor(int tileId) => _fgColorCache.GetValueOrDefault(tileId);

    /// <summary>Returns the background color (packed 0xRRGGBB) for a tile.</summary>
    public int GetBgColor(int tileId) => _bgColorCache.GetValueOrDefault(tileId);
}
