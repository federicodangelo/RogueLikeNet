using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a showcase with a 5x5 room for each enemy type at multiple difficulty levels.
/// Layout: rows = enemy types, columns = difficulty tiers (0, 2, 5, 10).
/// Each room contains one enemy of that type scaled to that difficulty.
/// Only chunk (0,0) has content; other chunks are empty floors.
/// </summary>
public class EnemyShowcaseGenerator : IDungeonGenerator
{
    private static readonly int[] DifficultyTiers = [0, 1, 2, 3, 5, 8, 10];

    private readonly long _seed;

    public EnemyShowcaseGenerator(long seed)
    {
        _seed = seed;
    }

    public bool Exists(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

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

        // Layout constants
        // stride = roomSize + 1 (west wall) + 1 (east wall) + 2 (gap) = roomSize + 4
        const int roomSize = 5;
        const int strideX = 9;  // roomSize + 4
        const int strideY = 9;
        const int startX = 2;
        const int startY = 4;   // north wall of first row at y = startY-1 = 3

        // Spawn: center column, 2 tiles above the first row of rooms
        int spawnX = startX + 3 * strideX + roomSize / 2; // column 3 (middle of 7)
        result.SpawnPosition = Position.FromCoords(worldOffsetX + spawnX, worldOffsetY + 1, chunkZ);

        // Broad torch at spawn for initial visibility
        result.Elements.Add(new DungeonElement(
            Position.FromCoords(worldOffsetX + spawnX, worldOffsetY + 1, chunkZ),
            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
            new LightSource(30, TileDefinitions.ColorTorchFg)));

        for (int enemyIdx = 0; enemyIdx < NpcDefinitions.All.Length; enemyIdx++)
        {
            var def = NpcDefinitions.All[enemyIdx];

            for (int diffIdx = 0; diffIdx < DifficultyTiers.Length; diffIdx++)
            {
                int difficulty = DifficultyTiers[diffIdx];
                int rx = startX + diffIdx * strideX;
                int ry = startY + enemyIdx * strideY;

                if (rx + roomSize >= Chunk.Size - 1 || ry + roomSize >= Chunk.Size - 1)
                    continue;

                // Walled 5x5 room with 3-tile north opening
                BuildRoom(chunk, rx, ry, roomSize, roomSize);

                // Torch inside room so player can see through the doorway
                result.Elements.Add(new DungeonElement(
                    Position.FromCoords(worldOffsetX + rx + roomSize / 2, worldOffsetY + ry + roomSize / 2, chunkZ),
                    new TileAppearance(TileDefinitions.GlyphTorch, def.Color),
                    new LightSource(5, def.Color)));

                // Enemy in center
                int cx = rx + roomSize / 2;
                int cy = ry + roomSize / 2;
                var monsterData = NpcDefinitions.GenerateMonsterData(def, difficulty);
                result.Monsters.Add((Position.FromCoords(worldOffsetX + cx, worldOffsetY + cy, chunkZ), monsterData));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a walled room with a 3-tile centered opening on the north side.
    /// Interior tiles remain as pre-filled floor. Walls are placed on all 4 sides;
    /// the north wall has a gap of 3 tiles in the center.
    /// </summary>
    private static void BuildRoom(Chunk chunk, int rx, int ry, int rw, int rh)
    {
        int openStart = (rw - 3) / 2; // local x (from rx) where opening starts

        // North wall with centered 3-tile opening
        for (int x = rx - 1; x <= rx + rw; x++)
        {
            int lx = x - rx; // -1 .. rw
            if (lx < openStart || lx > openStart + 2)
                SetWall(chunk, x, ry - 1);
        }
        // South wall (full)
        for (int x = rx - 1; x <= rx + rw; x++)
            SetWall(chunk, x, ry + rh);
        // West and east walls
        for (int y = ry; y <= ry + rh; y++)
        {
            SetWall(chunk, rx - 1, y);
            SetWall(chunk, rx + rw, y);
        }
    }

    private static void SetWall(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Blocked;
        tile.GlyphId = TileDefinitions.GlyphWall;
        tile.FgColor = TileDefinitions.ColorWallFg;
        tile.BgColor = TileDefinitions.ColorBlack;
    }
}
