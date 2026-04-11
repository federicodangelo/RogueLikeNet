using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a large open arena with waves of enemies and loot.
/// The spawn chunk is a big walled arena with enemies and items scattered inside.
/// Good for testing combat mechanics. Other chunks are empty floors.
/// </summary>
public class ArenaGenerator : IDungeonGenerator
{
    private readonly long _seed;

    public ArenaGenerator(long seed)
    {
        _seed = seed;
    }

    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        int floorTileId = GameData.Instance.Tiles.GetNumericId("floor");
        int wallTileId = GameData.Instance.Tiles.GetNumericId("wall");

        // Fill with floor
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = floorTileId;

        if (chunkX != 0 || chunkY != 0)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        var rng = new SeededRandom(_seed);

        // Spawn point: center of the arena
        result.SpawnPosition = Position.FromCoords(worldOffsetX + Chunk.Size / 2, worldOffsetY + Chunk.Size / 2, chunkZ);

        // Build walls around the entire chunk perimeter
        for (int x = 0; x < Chunk.Size; x++)
        {
            chunk.Tiles[x, 0].TileId = wallTileId;
            chunk.Tiles[x, Chunk.Size - 1].TileId = wallTileId;
        }
        for (int y = 0; y < Chunk.Size; y++)
        {
            chunk.Tiles[0, y].TileId = wallTileId;
            chunk.Tiles[Chunk.Size - 1, y].TileId = wallTileId;
        }

        // Place 4 pillars for cover
        int[] pillarXs = [16, 48, 16, 48];
        int[] pillarYs = [16, 16, 48, 48];
        for (int p = 0; p < 4; p++)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int px = pillarXs[p] + dx, py = pillarYs[p] + dy;
                    if (px >= 0 && px < Chunk.Size && py >= 0 && py < Chunk.Size)
                        chunk.Tiles[px, py].TileId = wallTileId;
                }
        }

        // Torches in the corners and center
        int[][] torchPositions =
        [
            [8, 8], [56, 8], [8, 56], [56, 56], [32, 32],
            [32, 8], [32, 56], [8, 32], [56, 32]
        ];
        foreach (var pos in torchPositions)
        {
            result.Elements.Add(new DungeonElement(
                Position.FromCoords(worldOffsetX + pos[0], worldOffsetY + pos[1], chunkZ),
                new TileAppearance(RenderConstants.GlyphTorch, RenderConstants.ColorTorchFg),
                new LightSource(10, RenderConstants.ColorTorchFg)));
        }

        // Spawn enemies scattered around
        int difficulty = 2;
        int monsterCount = 20;
        for (int m = 0; m < monsterCount; m++)
        {
            int x = 3 + rng.Next(Chunk.Size - 6);
            int y = 3 + rng.Next(Chunk.Size - 6);

            if (chunk.Tiles[x, y].Type != TileType.Floor) continue;

            int distToCenter = Math.Abs(x - 32) + Math.Abs(y - 32);
            if (distToCenter < 8) continue;

            var def = GameData.Instance.Npcs.Pick(rng, difficulty);
            var monsterData = NpcRegistry.GenerateMonsterData(def!, difficulty);
            result.Monsters.Add((Position.FromCoords(worldOffsetX + x, worldOffsetY + y, chunkZ), monsterData));
        }

        // Scatter some loot around
        int itemCount = 10;
        for (int i = 0; i < itemCount; i++)
        {
            int x = 3 + rng.Next(Chunk.Size - 6);
            int y = 3 + rng.Next(Chunk.Size - 6);
            if (chunk.Tiles[x, y].Type != TileType.Floor) continue;

            var loot = LootGenerator.GenerateLoot(rng, difficulty);
            var itemData = LootGenerator.GenerateItemData(loot.Definition, rng);
            result.Items.Add((Position.FromCoords(worldOffsetX + x, worldOffsetY + y, chunkZ), itemData));
        }

        return result;
    }
}
