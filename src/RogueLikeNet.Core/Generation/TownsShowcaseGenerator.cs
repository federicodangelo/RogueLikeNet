using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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

    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // Cycle through biomes based on chunk position
        var biomes = (BiomeType[])Enum.GetValues(typeof(BiomeType));
        long hash = (chunkX * 48611L ^ chunkY * 29423L) & 0x7FFFFFFFL;
        var biome = biomes[(int)(hash % biomes.Length)];

        int floorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = floorTileId;


        var rng = TownGenerator.GetSeededRandomForChunk(chunkPos, _seed);
        TownGenerator.Generate(chunk, result, rng, biome, worldOffsetX, worldOffsetY, chunkZ);

        // Spawn point for origin chunk
        if (chunkX == 0 && chunkY == 0)
        {
            result.SpawnPosition = Position.FromCoords(worldOffsetX + 2, worldOffsetY + 2, chunkZ);

            chunk.Tiles[2, 2].PlaceableItemId = GameData.Instance.Items.GetNumericId("torch_placeable");
        }

        return result;
    }
}
