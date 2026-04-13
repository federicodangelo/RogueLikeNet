namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a town type with composition rules for which structures to place.
/// Loaded from JSON. Can be restricted to specific biomes via BiomeOverrides.
/// </summary>
public sealed class TownDefinition : BaseDefinition
{
    public int MinTownSize { get; set; } = 25;
    public int MaxTownSize { get; set; } = 45;
    public int MinNpcs { get; set; } = 4;
    public int MaxNpcs { get; set; } = 8;
    public TownStructureRule[] Structures { get; set; } = [];
    public string[]? BiomeOverrides { get; set; }
}

public sealed class TownStructureRule
{
    public string Category { get; set; } = "";
    public string[]? StructureIds { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; } = 1;
    public int Weight { get; set; } = 50;
}
