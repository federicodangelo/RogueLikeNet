using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

public interface IDungeonGenerator
{
    bool Exists(ChunkPosition chunkPos);

    GenerationResult Generate(ChunkPosition chunkPos);
}

public class GenerationResult
{
    public Chunk Chunk { get; }
    public List<(Position Position, MonsterData Monster)> Monsters { get; } = new();
    public List<(Position Position, ItemData Item)> Items { get; } = new();
    public List<(Position Position, Data.ResourceNodeDefinition NodeDef)> ResourceNodes { get; } = new();
    public List<(Position Position, Data.AnimalDefinition AnimalDef)> Animals { get; } = new();
    public List<(Position Position, Data.ItemDefinition ItemSeed, int GrowthTicksCurrent, bool IsWatered)> Crops { get; } = new();
    public List<(Position Position, string Name, int TownCenterX, int TownCenterY, int WanderRadius)> TownNpcs { get; } = new();

    /// <summary>
    /// Suggested world-space spawn position for the player.
    /// Null means the engine should use its default floor-scan fallback.
    /// Only meaningful for chunk (0,0,OverworldZ) — other chunks are ignored.
    /// </summary>
    public Position? SpawnPosition { get; set; }

    /// <summary>
    /// Raw JSON entity data from a saved chunk, to be deserialized after the chunk
    /// is registered in the WorldMap (so Spawn* methods can find it via TryGetChunk).
    /// </summary>
    public string? RawEntityJson { get; set; }

    public GenerationResult(Chunk chunk)
    {
        Chunk = chunk;
    }

    public bool HasAnythingAt(Position pos)
    {
        return Monsters.Any(m => m.Position == pos) ||
               Items.Any(i => i.Position == pos) ||
               ResourceNodes.Any(n => n.Position == pos) ||
               Animals.Any(a => a.Position == pos) ||
               Crops.Any(c => c.Position == pos) ||
               TownNpcs.Any(t => t.Position == pos);
    }
}
