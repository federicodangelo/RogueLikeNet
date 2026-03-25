namespace RogueLikeNet.Core.Generation;

public interface IDungeonGenerator
{
    /// <summary>
    /// Generates terrain for a chunk and returns spawn points for entities.
    /// </summary>
    GenerationResult Generate(World.Chunk chunk, long seed);
}

/// <summary>
/// Describes a position where an entity should be spawned after generation.
/// </summary>
public readonly record struct SpawnPoint(int LocalX, int LocalY, SpawnType Type);

public enum SpawnType
{
    Monster,
    Item,
    Torch,
}

public class GenerationResult
{
    public List<SpawnPoint> SpawnPoints { get; } = new();
}
