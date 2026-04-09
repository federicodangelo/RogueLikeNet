using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Dispatches to a biome-appropriate dungeon generator for each chunk.
/// <list type="bullet">
///   <item>BSP rooms+corridors — Stone, Arcane, Crypt, Ruined</item>
///   <item>Cellular-automata caves — Lava, Forest, Fungal, Infernal</item>
///   <item>Directional tunnels — Ice, Sewer</item>
/// </list>
/// </summary>
public class BiomeDungeonGenerator : IDungeonGenerator
{
    private readonly BspDungeonGenerator _bsp;
    private readonly CellularAutomataCaveGenerator _cave;
    private readonly DirectionalTunnelGenerator _tunnel;
    private readonly long _seed;

    public BiomeDungeonGenerator(long seed)
    {
        _seed = seed;
        _bsp = new BspDungeonGenerator(seed);
        _cave = new CellularAutomataCaveGenerator(seed);
        _tunnel = new DirectionalTunnelGenerator(seed);
    }

    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var biome = BiomeRegistry.GetBiomeForChunk(chunkPos, _seed);
        var generator = PickGenerator(biome);
        return generator.Generate(chunkPos);
    }

    private IDungeonGenerator PickGenerator(BiomeType biome) => biome switch
    {
        // Structured dungeons — BSP rooms connected by corridors
        BiomeType.Stone => _bsp,
        BiomeType.Arcane => _bsp,
        BiomeType.Crypt => _bsp,
        BiomeType.Ruined => _bsp,

        // Organic caves — cellular automata
        BiomeType.Lava => _cave,
        BiomeType.Forest => _cave,
        BiomeType.Fungal => _cave,
        BiomeType.Infernal => _cave,

        // Linear passages — directional tunnels
        BiomeType.Ice => _tunnel,
        BiomeType.Sewer => _tunnel,

        _ => _bsp,
    };
}
