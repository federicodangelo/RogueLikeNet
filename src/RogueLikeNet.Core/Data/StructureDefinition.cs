using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a placeable structure template (house, shop, farm, etc.) loaded from JSON.
/// Uses an ASCII grid + legend system: each character in the grid maps to an item or special key.
/// </summary>
public sealed class StructureDefinition : BaseDefinition
{
    public const string BiomeWallKey = "biome_wall";
    public const string BiomeDoorKey = "biome_door";
    public const string BiomeWindowKey = "biome_window";
    public const string BiomeFloorKey = "biome_floor";
    public const string EmptyKey = "empty";

    // Validate structures
    static public readonly HashSet<string> SpecialLegendValues = new(StringComparer.Ordinal) {
        BiomeWallKey,
        BiomeDoorKey,
        BiomeWindowKey,
        BiomeFloorKey,
        EmptyKey
    };

    public string Category { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string[] Grid { get; set; } = [];
    public Dictionary<string, string> Legend { get; set; } = new();
    public StructureNpcDef[] Npcs { get; set; } = [];
    public StructureGroundItemDef[] GroundItems { get; set; } = [];
    public string[]? CropGrid { get; set; }
    public Dictionary<string, string>? CropLegend { get; set; }
    public string[]? AnimalGrid { get; set; }
    public Dictionary<string, string>? AnimalLegend { get; set; }
    public bool AllowRotation { get; set; } = true;
}

public sealed class StructureNpcDef
{
    public int X { get; set; }
    public int Y { get; set; }
    public TownNpcRole Role { get; set; } = TownNpcRole.Villager;
}

public sealed class StructureGroundItemDef
{
    public int X { get; set; }
    public int Y { get; set; }
    public string ItemId { get; set; } = "";
}
