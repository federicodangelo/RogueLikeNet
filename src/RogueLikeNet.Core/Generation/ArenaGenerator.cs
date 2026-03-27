using RogueLikeNet.Core.Components;
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

    public GenerationResult Generate(int chunkX, int chunkY)
    {
        var chunk = new Chunk(chunkX, chunkY);
        var result = new GenerationResult(chunk);

        // Fill with floor
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }

        if (chunkX != 0 || chunkY != 0)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        var rng = new SeededRandom(_seed);

        // Spawn point: center of the arena
        result.SpawnPosition = (worldOffsetX + Chunk.Size / 2, worldOffsetY + Chunk.Size / 2);

        // Build walls around the entire chunk perimeter
        for (int x = 0; x < Chunk.Size; x++)
        {
            SetWall(chunk, x, 0);
            SetWall(chunk, x, Chunk.Size - 1);
        }
        for (int y = 0; y < Chunk.Size; y++)
        {
            SetWall(chunk, 0, y);
            SetWall(chunk, Chunk.Size - 1, y);
        }

        // Place 4 pillars for cover
        int[] pillarXs = [16, 48, 16, 48];
        int[] pillarYs = [16, 16, 48, 48];
        for (int p = 0; p < 4; p++)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    SetWall(chunk, pillarXs[p] + dx, pillarYs[p] + dy);
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
                new Position(worldOffsetX + pos[0], worldOffsetY + pos[1]),
                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                new LightSource(10, TileDefinitions.ColorTorchFg)));
        }

        // Spawn enemies scattered around
        int difficulty = 2;
        int monsterCount = 20;
        for (int m = 0; m < monsterCount; m++)
        {
            int x = 3 + rng.Next(Chunk.Size - 6);
            int y = 3 + rng.Next(Chunk.Size - 6);

            // Skip pillars area
            if (chunk.Tiles[x, y].Type != TileType.Floor) continue;

            // Don't spawn near player start (center area)
            int distToCenter = Math.Abs(x - 32) + Math.Abs(y - 32);
            if (distToCenter < 8) continue;

            var def = NpcDefinitions.Pick(rng, difficulty);
            int hpScale = 1 + difficulty / 2;
            result.Monsters.Add((new Position(worldOffsetX + x, worldOffsetY + y), new MonsterData
            {
                MonsterTypeId = def.TypeId,
                Health = def.Health * hpScale,
                Attack = def.Attack + difficulty,
                Defense = def.Defense + difficulty / 2,
                Speed = def.Speed,
            }));
        }

        // Scatter some loot around
        int itemCount = 10;
        for (int i = 0; i < itemCount; i++)
        {
            int x = 3 + rng.Next(Chunk.Size - 6);
            int y = 3 + rng.Next(Chunk.Size - 6);
            if (chunk.Tiles[x, y].Type != TileType.Floor) continue;

            var loot = ItemDefinitions.GenerateLoot(rng, difficulty);
            int rarityMult = 100 + loot.Rarity * 50;
            result.Items.Add((new Position(worldOffsetX + x, worldOffsetY + y), new ItemData
            {
                ItemTypeId = loot.Definition.TypeId,
                Rarity = loot.Rarity,
                BonusAttack = loot.Definition.BaseAttack * rarityMult / 100,
                BonusDefense = loot.Definition.BaseDefense * rarityMult / 100,
                BonusHealth = loot.Definition.BaseHealth * rarityMult / 100,
                StackCount = loot.Definition.Stackable
                    ? (loot.Definition.Category == ItemDefinitions.CategoryGold ? 10 + rng.Next(50) : 1)
                    : 1,
            }));
        }

        return result;
    }

    private static void SetWall(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Wall;
        tile.GlyphId = TileDefinitions.GlyphWall;
        tile.FgColor = TileDefinitions.ColorWallFg;
        tile.BgColor = TileDefinitions.ColorBlack;
    }
}
