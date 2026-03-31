using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Registry of all available dungeon generators.
/// Each entry has a display name and a factory that creates the generator from a seed.
/// </summary>
public static class GeneratorRegistry
{
    public static readonly GeneratorEntry[] All =
    [
        new("Overworld", seed => new OverworldGenerator(seed)),
        new("Biome Dungeons", seed => new BiomeDungeonGenerator(seed)),
        new("BSP Dungeon", seed => new BspDungeonGenerator(seed)),
        new("Cave (Cellular)", seed => new CellularAutomataCaveGenerator(seed)),
        new("Tunnel", seed => new DirectionalTunnelGenerator(seed)),
        new("Empty", seed => new EmptyGenerator()),
        new("Item Showcase", seed => new ItemShowcaseGenerator(seed)),
        new("Enemy Showcase", seed => new EnemyShowcaseGenerator(seed)),
        new("Biome Showcase", seed => new BiomeShowcaseGenerator(seed)),
        new("Arena", seed => new ArenaGenerator(seed)),
        new("Towns Showcase", seed => new TownsShowcaseGenerator(seed)),
        new ("MultiLevel Showcase", seed => new MultiLevelDungeonGenerator(seed)),
    ];

    public static int Count => All.Length;

    /// <summary>Default generator index (Overworld).</summary>
    public const int DefaultIndex = 0;

    /// <summary>Creates a generator by registry index.</summary>
    public static IDungeonGenerator Create(int index, long seed)
    {
        if (index < 0 || index >= All.Length)
            index = DefaultIndex;
        return All[index].Factory(seed);
    }

    /// <summary>Gets the display name for a generator index.</summary>
    public static string GetName(int index)
    {
        if (index < 0 || index >= All.Length)
            index = DefaultIndex;
        return All[index].Name;
    }
}

public readonly record struct GeneratorEntry(string Name, Func<long, IDungeonGenerator> Factory);
