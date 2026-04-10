using System.Text.Json.Serialization;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a biome with its terrain, decorations, enemies, and liquid features.
/// Loaded from JSON data files.
/// </summary>
public sealed class BiomeDefinition : BaseDefinition
{
    public int FloorColor { get; set; }
    public int TintR { get; set; } = 100;
    public int TintG { get; set; } = 100;
    public int TintB { get; set; } = 100;
    public BiomeDecorationDef[] Decorations { get; set; } = [];
    public BiomeEnemySpawnDef[] EnemySpawns { get; set; } = [];
    public BiomeLiquidDef? Liquid { get; set; }
    public BiomeResourceWeight[] ResourceWeights { get; set; } = [];
}

public sealed class BiomeDecorationDef
{
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int Chance1000 { get; set; }
}

public sealed class BiomeEnemySpawnDef
{
    public string NpcId { get; set; } = "";
    public int Weight { get; set; }
}

public sealed class BiomeLiquidDef
{
    public TileType TileType { get; set; }
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int BgColor { get; set; }
    public int Chance100 { get; set; }
}

public sealed class BiomeResourceWeight
{
    public string NodeId { get; set; } = "";
    public int Weight { get; set; }
}
