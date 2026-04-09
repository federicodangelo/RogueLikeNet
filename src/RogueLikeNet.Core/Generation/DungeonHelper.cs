using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Shared helpers used by all dungeon generator implementations.
/// Handles tile carving, feature placement, population, decorations, liquids, and biome tinting.
/// </summary>
internal static class DungeonHelper
{
    public static void FillWalls(Chunk chunk)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Blocked;
                tile.GlyphId = TileDefinitions.GlyphWall;
                tile.FgColor = TileDefinitions.ColorWallFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
    }

    public static void CarveTile(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type == TileType.Blocked)
        {
            tile.Type = TileType.Floor;
            tile.GlyphId = TileDefinitions.GlyphFloor;
            tile.FgColor = TileDefinitions.ColorFloorFg;
            tile.BgColor = TileDefinitions.ColorBlack;
        }
    }

    public static void CarveFloor(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Floor;
        tile.GlyphId = TileDefinitions.GlyphFloor;
        tile.FgColor = TileDefinitions.ColorFloorFg;
        tile.BgColor = TileDefinitions.ColorBlack;
    }

    public static void CarveRoom(Chunk chunk, Room room)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                CarveFloor(chunk, x, y);
    }

    public static void PlaceFeature(Chunk chunk, int x, int y, TileType type, int glyph, int fgColor)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = type;
        tile.GlyphId = glyph;
        tile.FgColor = fgColor;
    }

    public static void PlaceStairs(Chunk chunk, List<Room> rooms)
    {
        if (rooms.Count < 2)
        {
            return;
        }


        ;
        var first = rooms[0];
        var last = rooms[^1];
        PlaceFeature(chunk, first.CenterX, first.CenterY, TileType.StairsUp,
            TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);
        PlaceFeature(chunk, last.CenterX, last.CenterY, TileType.StairsDown,
            TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
    }

    public static void PlaceLiquidPools(Chunk chunk, List<Room> rooms, BiomeType biome, SeededRandom rng)
    {
        var liquidDef = BiomeDefinitions.GetLiquid(biome);
        if (liquidDef == null) return;
        var liq = liquidDef.Value;

        for (int i = 1; i < rooms.Count - 1; i++)
        {
            if (rng.Next(100) >= liq.Chance100RoomBecomesLiquid) continue;
            var room = rooms[i];
            if (room.Width < 6 || room.Height < 6) continue;
            bool circular = rng.NextBool();

            int poolX = room.X + 2;
            int poolY = room.Y + 2;
            int poolW = room.Width - 4;
            int poolH = room.Height - 4;
            int poolCenterX = poolX + poolW / 2;
            int poolCenterY = poolY + poolH / 2;

            for (int x = poolX; x < poolX + poolW; x++)
            {
                for (int y = poolY; y < poolY + poolH; y++)
                {
                    if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
                    {
                        if (circular && Position.ManhattanDistance2D(x, y, poolCenterX, poolCenterY) > Math.Min(poolW, poolH) / 2)
                            continue; // Make the pool roughly circular

                        ref var tile = ref chunk.Tiles[x, y];
                        tile.Type = liq.Type;
                        tile.GlyphId = liq.GlyphId;
                        tile.FgColor = liq.FgColor;
                        tile.BgColor = liq.BgColor;
                    }
                }
            }
        }
    }

    public static void PlaceDecorations(Chunk chunk, BiomeType biome, SeededRandom rng)
    {
        var decorations = BiomeDefinitions.GetDecorations(biome);
        if (decorations.Length == 0) return;

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                if (tile.Type != TileType.Floor) continue;

                foreach (var deco in decorations)
                {
                    if (rng.Next(1000) < deco.Chance1000)
                    {
                        tile.Type = TileType.Floor;
                        tile.GlyphId = deco.GlyphId;
                        tile.FgColor = deco.FgColor;
                        break;
                    }
                }
            }
        }
    }

    public static void PopulateRoom(Room room, SeededRandom rng, GenerationResult result, int difficulty, int worldOffsetX, int worldOffsetY, int worldZ)
    {
        int monsterCount = 1 + rng.Next(3);

        bool IsChunkCoordinateWalkable(int cx, int cy)
        {
            if (cx < 0 || cx >= Chunk.Size || cy < 0 || cy >= Chunk.Size) return false;
            return result.Chunk.Tiles[cx, cy].IsWalkable;
        }

        bool FindRandomRoomWalkableCoordinate(out int x, out int y)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
                y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
                if (IsChunkCoordinateWalkable(x, y))
                    return true;
            }
            x = 0;
            y = 0;
            return false;
        }

        for (int i = 0; i < monsterCount; i++)
        {
            if (FindRandomRoomWalkableCoordinate(out var x, out var y))
            {
                var def = NpcDefinitions.Pick(rng, difficulty);
                var monsterData = NpcDefinitions.GenerateMonsterData(def, difficulty);
                int hpScale = 1 + difficulty / 2;
                result.Monsters.Add((Position.FromCoords(worldOffsetX + x, worldOffsetY + y, worldZ), monsterData));
            }
        }

        if (rng.Next(100) < 30)
        {
            if (FindRandomRoomWalkableCoordinate(out var x, out var y))
            {
                var loot = LootGenerator.GenerateLoot(rng, difficulty);
                result.Items.Add((Position.FromCoords(worldOffsetX + x, worldOffsetY + y, worldZ), new ItemData
                {
                    ItemTypeId = loot.Definition.NumericId,
                    StackCount = loot.Definition.Stackable
                        ? (loot.Definition.Category == ItemCategory.Misc ? 10 + rng.Next(50) : 1)
                        : 1,
                }));
            }
        }

        if (rng.Next(100) < 40)
        {
            if (IsChunkCoordinateWalkable(room.CenterX, room.CenterY))
            {
                result.Elements.Add(new DungeonElement(
                    Position.FromCoords(worldOffsetX + room.CenterX, worldOffsetY + room.CenterY, worldZ),
                    new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                    new LightSource(6, TileDefinitions.ColorTorchFg)));
            }
        }
    }

    public static void PopulateRooms(List<Room> rooms, SeededRandom rng, GenerationResult result, int difficulty, int worldOffsetX, int worldOffsetY, int worldZ)
    {
        for (int i = 1; i < rooms.Count; i++)
            PopulateRoom(rooms[i], rng, result, difficulty, worldOffsetX, worldOffsetY, worldZ);
    }

    public static void PlaceResourceNodes(List<Room> rooms, SeededRandom rng, GenerationResult result, BiomeType biome, int worldOffsetX, int worldOffsetY, int worldZ)
    {
        for (int i = 1; i < rooms.Count; i++)
        {
            int nodeCount = rng.Next(3); // 0-2 nodes per room
            var room = rooms[i];
            for (int n = 0; n < nodeCount; n++)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
                    int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
                    if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                    if (result.Chunk.Tiles[x, y].Type != TileType.Floor) continue;

                    var def = ResourceNodeDefinitions.Pick(rng, biome);
                    result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + x, worldOffsetY + y, worldZ), def));
                    break;
                }
            }
        }
    }

    public static void ApplyBiomeTint(Chunk chunk, BiomeType biome)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.FgColor = BiomeDefinitions.ApplyBiomeTint(tile.FgColor, biome);
                tile.BgColor = BiomeDefinitions.ApplyBiomeTint(tile.BgColor, biome);
            }
    }

    public static void CarveHLine(Chunk chunk, int x1, int x2, int y)
    {
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);
        for (int x = minX; x <= maxX; x++)
            CarveTile(chunk, x, y);
    }

    public static void CarveVLine(Chunk chunk, int y1, int y2, int x)
    {
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
            CarveTile(chunk, x, y);
    }

    public static void CarveCorridor(Chunk chunk, int x1, int y1, int x2, int y2, SeededRandom rng)
    {
        if (rng.Next(2) == 0)
        {
            CarveHLine(chunk, x1, x2, y1);
            CarveVLine(chunk, y1, y2, x2);
        }
        else
        {
            CarveVLine(chunk, y1, y2, x1);
            CarveHLine(chunk, x1, x2, y2);
        }
    }

    public static (int X, int Y)? FindSpawnPoint(Chunk chunk)
    {
        int worldOffsetX = chunk.ChunkPosition.X * Chunk.Size;
        int worldOffsetY = chunk.ChunkPosition.Y * Chunk.Size;
        int midX = Chunk.Size / 2;
        int midY = Chunk.Size / 2;
        // Spiral outward from center until we find an open floor tile
        for (int radius = 0; radius < Chunk.Size / 2; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    int cx = midX + dx, cy = midY + dy;
                    if (cx < 1 || cy < 1 || cx >= Chunk.Size - 1 || cy >= Chunk.Size - 1) continue;
                    if (chunk.Tiles[cx, cy].Type == TileType.Floor)
                    {
                        return (worldOffsetX + cx, worldOffsetY + cy);
                    }
                }
            }
        }
        return null;
    }

    public static void RemoveEnemiesInRadius(GenerationResult result, int x, int y, int radius)
    {
        result.Monsters.RemoveAll(m =>
            Position.ManhattanDistance2D(m.Position.X, m.Position.Y, x, y) <= radius
        );
    }

    public static void MakeTilesFloorInRadius(GenerationResult result, int x, int y, int radius)
    {
        var chunk = result.Chunk;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                int cx = x + dx, cy = y + dy;
                if (Position.ManhattanDistance2D(x, y, cx, cy) > radius)
                    continue;
                if (cx < 0 || cy < 0 || cx >= Chunk.Size || cy >= Chunk.Size)
                    continue;
                chunk.Tiles[cx, cy].Type = TileType.Floor;
                chunk.Tiles[cx, cy].GlyphId = TileDefinitions.GlyphFloor;
                chunk.Tiles[cx, cy].FgColor = TileDefinitions.ColorFloorFg;
                chunk.Tiles[cx, cy].BgColor = TileDefinitions.ColorBlack;
            }
        }
    }
}

/// <summary>Axis-aligned room within a chunk.</summary>
internal class Room
{
    public int X, Y, Width, Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public Room(int x, int y, int w, int h)
    {
        X = x; Y = y; Width = w; Height = h;
    }
}
