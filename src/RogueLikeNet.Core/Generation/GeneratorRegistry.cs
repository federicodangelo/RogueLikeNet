using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Registry of all available dungeon generators.
/// Each entry has a stable string ID, display name, and a factory that creates the generator from a seed.
/// </summary>
public static class GeneratorRegistry
{
    public static readonly GeneratorEntry[] All =
    [
        new("overworld", "Overworld", seed => new OverworldGenerator(seed)),
        new("biome-dungeons", "Biome Dungeons", seed => new BiomeDungeonGenerator(seed)),
        new("bsp-dungeon", "BSP Dungeon", seed => new BspDungeonGenerator(seed)),
        new("cave-cellular", "Cave (Cellular)", seed => new CellularAutomataCaveGenerator(seed)),
        new("tunnel", "Tunnel", seed => new DirectionalTunnelGenerator(seed)),
        new("empty", "Empty", seed => new EmptyGenerator()),
        new("item-showcase", "Item Showcase", seed => new ItemShowcaseGenerator(seed)),
        new("enemy-showcase", "Enemy Showcase", seed => new EnemyShowcaseGenerator(seed)),
        new("biome-showcase", "Biome Showcase", seed => new BiomeShowcaseGenerator(seed)),
        new("arena", "Arena", seed => new ArenaGenerator(seed)),
        new("towns-showcase", "Towns Showcase", seed => new TownsShowcaseGenerator(seed)),
        new("multilevel-showcase", "MultiLevel Showcase", seed => new MultiLevelDungeonGenerator(seed)),
    ];

    public static int Count => All.Length;

    /// <summary>Default generator index (Overworld).</summary>
    public const int DefaultIndex = 0;

    /// <summary>Default generator string ID.</summary>
    public const string DefaultId = "overworld";

    /// <summary>Creates a generator by registry index.</summary>
    public static IDungeonGenerator Create(int index, long seed)
    {
        if (index < 0 || index >= All.Length)
            index = DefaultIndex;
        return All[index].Factory(seed);
    }

    /// <summary>Creates a generator by its stable string ID. Falls back to default if not found.</summary>
    public static IDungeonGenerator Create(string id, long seed)
    {
        var index = GetIndex(id);
        return All[index].Factory(seed);
    }

    /// <summary>Gets the display name for a generator index.</summary>
    public static string GetName(int index)
    {
        if (index < 0 || index >= All.Length)
            index = DefaultIndex;
        return All[index].Name;
    }

    /// <summary>Gets the stable string ID for a generator index.</summary>
    public static string GetId(int index)
    {
        if (index < 0 || index >= All.Length)
            index = DefaultIndex;
        return All[index].Id;
    }

    /// <summary>Gets the registry index for a stable string ID. Returns DefaultIndex if not found.</summary>
    public static int GetIndex(string id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return DefaultIndex;
    }

    /// <summary>Gets the display name for a stable string ID. Returns the raw ID if not found.</summary>
    public static string GetNameOrId(string id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return All[i].Name;
        }
        return id;
    }
}

public readonly record struct GeneratorEntry(string Id, string Name, Func<long, IDungeonGenerator> Factory);
