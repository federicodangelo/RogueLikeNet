using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

public interface IDungeonGenerator
{
    GenerationResult Generate(int chunkX, int chunkY);
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

    /// <summary>
    /// Suggested world-space spawn position for the player.
    /// Null means the engine should use its default floor-scan fallback.
    /// Only meaningful for chunk (0,0) — other chunks are ignored.
    /// </summary>
    public (int X, int Y)? SpawnPosition { get; set; }

    public GenerationResult(Chunk chunk)
    {
        Chunk = chunk;
    }
}
