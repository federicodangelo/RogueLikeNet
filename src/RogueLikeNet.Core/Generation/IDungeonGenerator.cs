using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

public interface IDungeonGenerator
{
    bool Exists(Position chunkPos);

    GenerationResult Generate(Position chunkPos);
}

/// <summary>
/// An element placed in the dungeon with a visual appearance and optional light source.
/// </summary>
public readonly record struct DungeonElement(Position Position, TileAppearance Appearance, LightSource? Light);

public class GenerationResult
{
    public Chunk Chunk { get; }
    public List<(Position Position, MonsterData Monster)> Monsters { get; } = new();
    public List<(Position Position, ItemData Item)> Items { get; } = new();
    public List<DungeonElement> Elements { get; } = new();
    public List<(Position Position, ResourceNodeDefinition NodeDef)> ResourceNodes { get; } = new();
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
}
