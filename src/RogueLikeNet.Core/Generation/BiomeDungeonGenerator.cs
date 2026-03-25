using RogueLikeNet.Core.Definitions;
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
    private readonly BspDungeonGenerator _bsp = new();
    private readonly CellularAutomataCaveGenerator _cave = new();
    private readonly DirectionalTunnelGenerator _tunnel = new();

    public GenerationResult Generate(Chunk chunk, long seed)
    {
        var biome = BiomeDefinitions.GetBiomeForChunk(chunk.ChunkX, chunk.ChunkY, seed);
        var generator = PickGenerator(biome);
        return generator.Generate(chunk, seed);
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
