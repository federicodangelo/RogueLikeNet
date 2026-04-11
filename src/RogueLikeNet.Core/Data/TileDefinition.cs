using System.Text.Json.Serialization;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a single tile type with its visual appearance and properties.
/// Loaded from JSON data files (data/tiles/*.json).
/// </summary>
public sealed class TileDefinition : BaseDefinition
{
    public TileType Type { get; set; }

    [JsonConverter(typeof(GlyphConverter))]
    public int GlyphId { get; set; }

    public int FgColor { get; set; }
    public int BgColor { get; set; }
    public bool Walkable { get; set; }
    public bool Transparent { get; set; } = true;
}
