using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// Wraps a base IDungeonGenerator to load previously-saved chunks from persistence.
/// If a chunk was saved, tile data is restored from the provider and entities are
/// deserialized after the chunk is loaded. If not saved, delegates to the base generator.
/// </summary>
public class PersistentDungeonGenerator : IDungeonGenerator
{
    private readonly IDungeonGenerator _baseGenerator;
    private readonly ISaveGameProvider _provider;
    private readonly string _slotId;

    public PersistentDungeonGenerator(IDungeonGenerator baseGenerator, ISaveGameProvider provider, string slotId)
    {
        _baseGenerator = baseGenerator;
        _provider = provider;
        _slotId = slotId;
    }

    public bool Exists(ChunkPosition chunkPos)
    {
        // If we have a saved chunk, it definitely exists
        var saved = _provider.LoadChunk(_slotId, chunkPos);
        if (saved != null)
            return true;

        return _baseGenerator.Exists(chunkPos);
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var saved = _provider.LoadChunk(_slotId, chunkPos);
        if (saved != null)
        {
            // Restore tiles from saved data
            var tiles = ChunkSerializer.DeserializeTiles(saved.TileData);
            var chunk = new Chunk(chunkPos);
            Array.Copy(tiles, chunk.Tiles, tiles.Length);
            var result = new GenerationResult(chunk);

            // Pass entity data through to ProcessGenerationResult, which runs
            // after the chunk is registered in WorldMap so Spawn* can find it.
            if (!string.IsNullOrEmpty(saved.EntityData) && saved.EntityData != "[]")
            {
                result.RawEntityJson = saved.EntityData;
            }

            return result;
        }

        // Not saved — generate fresh from base
        return _baseGenerator.Generate(chunkPos);
    }
}
