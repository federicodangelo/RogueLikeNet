using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

public enum BiomeType
{
    Stone,
    Lava,
    Ice,
    Forest,
    Arcane,
    Crypt,
    Sewer,
    Fungal,
    Ruined,
    Infernal,
}

/// <summary>
/// Defines a biome with its terrain tiles, decorations, enemies, and liquid features.
/// Loaded from JSON data files. Tile references are resolved to numeric IDs during registration.
/// </summary>
public sealed class BiomeDefinition : BaseDefinition
{
    public string FloorTileId { get; set; } = "";
    public string WallTileId { get; set; } = "";
    public BiomeDecorationDef[] Decorations { get; set; } = [];
    public BiomeEnemySpawnDef[] EnemySpawns { get; set; } = [];
    public BiomeLiquidDef? Liquid { get; set; }
    public BiomeResourceWeight[] ResourceWeights { get; set; } = [];

    // Town construction material item IDs (string, from JSON)
    public string TownWallItemId { get; set; } = "";
    public string TownDoorItemId { get; set; } = "";
    public string TownWindowItemId { get; set; } = "";
    public string TownFloorItemId { get; set; } = "";

    // Resolved numeric tile IDs (set during registration)
    [JsonIgnore] public int FloorTileNumericId { get; set; }
    [JsonIgnore] public int WallTileNumericId { get; set; }

    // Resolved numeric item IDs for town materials (set during registration)
    [JsonIgnore] public int TownWallItemNumericId { get; set; }
    [JsonIgnore] public int TownDoorItemNumericId { get; set; }
    [JsonIgnore] public int TownWindowItemNumericId { get; set; }
    [JsonIgnore] public int TownFloorItemNumericId { get; set; }
}

public sealed class BiomeDecorationDef
{
    public string TileId { get; set; } = "";
    public int Chance1000 { get; set; }

    // Resolved during registration
    [JsonIgnore] public int TileNumericId { get; set; }
}

public sealed class BiomeEnemySpawnDef
{
    public string NpcId { get; set; } = "";
    public int Weight { get; set; }
}

public sealed class BiomeLiquidDef
{
    public string TileId { get; set; } = "";
    public int Chance100 { get; set; }

    // Resolved during registration
    [JsonIgnore] public int TileNumericId { get; set; }
}

public sealed class BiomeResourceWeight
{
    public string NodeId { get; set; } = "";
    public int Weight { get; set; }
}
