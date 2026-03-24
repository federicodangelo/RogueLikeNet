namespace RogueLikeNet.Core.Generation;

public interface IDungeonGenerator
{
    void Generate(World.Chunk chunk, long seed);
}
