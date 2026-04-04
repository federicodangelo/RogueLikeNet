using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Showcase generator that places a town in every chunk on flat terrain.
/// Cycles through biomes so each chunk uses a different construction material.
/// </summary>
public class TownsShowcaseGenerator : IDungeonGenerator
{
    private readonly long _seed;

    public TownsShowcaseGenerator(long seed)
    {
        _seed = seed;
    }

    public bool Exists(int chunkX, int chunkY, int chunkZ)
    {
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(int chunkX, int chunkY, int chunkZ)
    {
        var chunk = new Chunk(chunkX, chunkY, chunkZ);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // Fill entire chunk with flat floor
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
        }

        // Cycle through biomes based on chunk position
        var biomes = (BiomeType[])Enum.GetValues(typeof(BiomeType));
        long hash = (chunkX * 48611L ^ chunkY * 29423L) & 0x7FFFFFFFL;
        var biome = biomes[(int)(hash % biomes.Length)];

        var rng = new SeededRandom(_seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678)));
        TownGenerator.Generate(chunk, result, rng, biome, worldOffsetX, worldOffsetY, chunkZ);

        // Spawn point for origin chunk
        if (chunkX == 0 && chunkY == 0)
        {
            result.SpawnPosition = Position.FromCoords(worldOffsetX + 2, worldOffsetY + 2, chunkZ);

            result.Elements.Add(new DungeonElement(
                Position.FromCoords(worldOffsetX + 2, worldOffsetY + 2, chunkZ),
                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                new LightSource(10, TileDefinitions.ColorTorchFg)));
        }

        return result;
    }
}
