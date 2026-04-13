using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Shared helpers used by all dungeon generator implementations.
/// Handles tile carving, feature placement, population, decorations, and liquids.
/// </summary>
internal static class DungeonHelper
{
    public static void FillWalls(Chunk chunk, int wallTileId)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = wallTileId;
    }

    public static void CarveTile(Chunk chunk, int x, int y, int floorTileId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type == TileType.Blocked)
            tile.TileId = floorTileId;
    }

    public static void CarveFloor(Chunk chunk, int x, int y, int floorTileId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        chunk.Tiles[x, y].TileId = floorTileId;
    }

    public static void CarveRoom(Chunk chunk, Room room, int floorTileId)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                CarveFloor(chunk, x, y, floorTileId);
    }

    public static void PlaceTile(Chunk chunk, int x, int y, int tileId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        chunk.Tiles[x, y].TileId = tileId;
    }

    public static void PlaceStairs(Chunk chunk, List<Room> rooms)
    {
        if (rooms.Count < 2)
            return;

        var first = rooms[0];
        var last = rooms[^1];
        int stairsUp = GameData.Instance.Tiles.GetNumericId("stairs_up");
        int stairsDown = GameData.Instance.Tiles.GetNumericId("stairs_down");
        PlaceTile(chunk, first.CenterX, first.CenterY, stairsUp);
        PlaceTile(chunk, last.CenterX, last.CenterY, stairsDown);
    }

    public static bool HasStairsAtBounds(Chunk chunk, int x, int y, int width, int height)
    {
        for (int cx = x; cx < x + width; cx++)
        {
            for (int cy = y; cy < y + height; cy++)
            {
                if (cx < 0 || cy < 0 || cx >= Chunk.Size || cy >= Chunk.Size) continue;
                var tileType = chunk.Tiles[cx, cy].Type;
                if (tileType == TileType.StairsUp || tileType == TileType.StairsDown)
                    return true;
            }
        }
        return false;
    }

    public static void PlaceLiquidPools(Chunk chunk, List<Room> rooms, BiomeType biome, SeededRandom rng)
    {
        var liquidDef = GameData.Instance.Biomes.GetLiquid(biome);
        if (liquidDef == null) return;
        int liquidTileId = liquidDef.TileNumericId;

        // Always skip first and last rooms
        for (int i = 1; i < rooms.Count - 1; i++)
        {
            if (rng.Next(100) >= liquidDef.Chance100) continue;
            var room = rooms[i];
            if (room.Width < 6 || room.Height < 6) continue;
            bool circular = rng.NextBool();

            int poolX = room.X + 2;
            int poolY = room.Y + 2;
            int poolW = room.Width - 4;
            int poolH = room.Height - 4;
            int poolCenterX = poolX + poolW / 2;
            int poolCenterY = poolY + poolH / 2;

            if (HasStairsAtBounds(chunk, poolX - 1, poolY - 1, poolW + 2, poolH + 2))
                continue;

            for (int x = poolX; x < poolX + poolW; x++)
            {
                for (int y = poolY; y < poolY + poolH; y++)
                {
                    if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
                    {
                        if (circular && Position.ManhattanDistance2D(x, y, poolCenterX, poolCenterY) > Math.Min(poolW, poolH) / 2)
                            continue;

                        chunk.Tiles[x, y].TileId = liquidTileId;
                    }
                }
            }
        }
    }

    public static void PlaceDecorations(Chunk chunk, BiomeType biome, SeededRandom rng, GenerationResult result)
    {
        var decorations = GameData.Instance.Biomes.GetDecorations(biome);
        if (decorations.Length == 0) return;

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                if (tile.Type != TileType.Floor || tile.HasPlaceable) continue;
                if (result.HasAnythingAt(chunk.LocalToWorld(x, y)))
                    continue;

                // Don't place decorations right next to each other to avoid clutter
                bool adjacentBusy = false;
                for (int ax = -1; ax <= 1; ax++)
                {
                    for (int ay = -1; ay <= 1; ay++)
                    {
                        int adjX = x + ax, adjY = y + ay;
                        if (adjX < 0 || adjY < 0 || adjX >= Chunk.Size || adjY >= Chunk.Size) continue;
                        if (result.HasAnythingAt(chunk.LocalToWorld(adjX, adjY)))
                        {
                            adjacentBusy = true;
                            break;
                        }
                        var adjTile = chunk.Tiles[adjX, adjY];
                        if (adjTile.HasPlaceable || adjTile.Type == TileType.StairsUp || adjTile.Type == TileType.StairsDown)
                        {
                            adjacentBusy = true;
                            break;
                        }
                    }
                    if (adjacentBusy) break;
                }
                if (adjacentBusy) continue;

                foreach (var deco in decorations)
                {
                    if (rng.Next(1000) < deco.Chance1000)
                    {
                        tile.TileId = deco.TileNumericId;
                        break;
                    }
                }
            }
        }
    }

    public static void PopulateRoom(Room room, SeededRandom rng, GenerationResult result, int difficulty, Position worldOffset, PopulateRoomsParams populateRoomsParams)
    {
        bool IsChunkCoordinateWalkable(int cx, int cy)
        {
            if (cx < 0 || cx >= Chunk.Size || cy < 0 || cy >= Chunk.Size) return false;

            // A chunk coordinate is walkable if it is walkable and its not near a stair (to avoid blocking access)
            var tile = result.Chunk.Tiles[cx, cy];
            if (!tile.IsWalkable || tile.Type == TileType.StairsUp || tile.Type == TileType.StairsDown)
                return false;

            if (result.HasAnythingAt(worldOffset.Offset(cx, cy)))
                return false;

            // Also check adjacent tiles to avoid placing monsters right next to stairs
            for (int ax = -1; ax <= 1; ax++)
            {
                for (int ay = -1; ay <= 1; ay++)
                {
                    int adjX = cx + ax, adjY = cy + ay;
                    if (adjX < 0 || adjY < 0 || adjX >= Chunk.Size || adjY >= Chunk.Size) continue;
                    var adjacentTile = result.Chunk.Tiles[adjX, adjY];
                    if (adjacentTile.Type == TileType.StairsUp || adjacentTile.Type == TileType.StairsDown)
                        return false;
                }
            }

            return true;
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

        // Add monsters
        if (rng.Next(100) < populateRoomsParams.RoomMonsterChanceBase100)
        {
            int monsterCount = rng.Next(populateRoomsParams.MinMonsters, populateRoomsParams.MaxMonsters + 1);
            for (int i = 0; i < monsterCount; i++)
            {
                if (FindRandomRoomWalkableCoordinate(out var x, out var y))
                {
                    var def = GameData.Instance.Npcs.Pick(rng, difficulty);
                    var monsterData = NpcRegistry.GenerateMonsterData(def!, difficulty);
                    result.Monsters.Add((worldOffset.Offset(x, y), monsterData));
                }
            }
        }

        // 30% chance to place a loot item in the room
        if (rng.Next(100) < populateRoomsParams.RoomLootChanceBase100)
        {
            if (FindRandomRoomWalkableCoordinate(out var x, out var y))
            {
                var loot = LootGenerator.GenerateLoot(rng, difficulty);
                result.Items.Add((worldOffset.Offset(x, y), new ItemData
                {
                    ItemTypeId = loot.Definition.NumericId,
                    StackCount = loot.Definition.Stackable
                        ? (loot.Definition.Category == ItemCategory.Misc ? 10 + rng.Next(50) : 1)
                        : 1,
                }));
            }
        }

        // 40% chance to place a torch decoration in the room
        if (rng.Next(100) < populateRoomsParams.RoomTorchChanceBase100)
        {
            if (FindRandomRoomWalkableCoordinate(out var torchX, out var torchY))
            {
                result.Chunk.Tiles[torchX, torchY].PlaceableItemId =
                    GameData.Instance.Items.GetNumericId("torch_placeable");
            }
        }
    }

    public class PopulateRoomsParams
    {
        public int RoomMonsterChanceBase100;
        public int MinMonsters;
        public int MaxMonsters;
        public int RoomLootChanceBase100;
        public int RoomTorchChanceBase100;

        static public PopulateRoomsParams Default => new PopulateRoomsParams
        {
            RoomMonsterChanceBase100 = 100,
            MinMonsters = 1,
            MaxMonsters = 3,
            RoomLootChanceBase100 = 30,
            RoomTorchChanceBase100 = 40
        };
    }

    public static void PopulateRooms(List<Room> rooms, SeededRandom rng, GenerationResult result, int difficulty, Position worldOffset, PopulateRoomsParams? populateRoomsParams = null)
    {
        if (populateRoomsParams == null)
            populateRoomsParams = PopulateRoomsParams.Default;
        for (int i = 1; i < rooms.Count; i++)
            PopulateRoom(rooms[i], rng, result, difficulty, worldOffset, populateRoomsParams);
    }

    public class PlaceResourceNodesParams
    {
        public int RoomNodesChanceBase100;
        public int MinNodes;
        public int MaxNodes;

        static public PlaceResourceNodesParams Default => new PlaceResourceNodesParams
        {
            RoomNodesChanceBase100 = 100,
            MinNodes = 1,
            MaxNodes = 2,
        };
    }

    public static void PlaceResourceNodes(List<Room> rooms, SeededRandom rng, GenerationResult result, BiomeType biome, Position worldOffset, PlaceResourceNodesParams? placeResourceNodesParams = null)
    {
        if (placeResourceNodesParams == null)
            placeResourceNodesParams = PlaceResourceNodesParams.Default;

        for (int i = 1; i < rooms.Count; i++)
        {
            if (rng.Next(100) < placeResourceNodesParams.RoomNodesChanceBase100)
            {
                int nodeCount = rng.Next(placeResourceNodesParams.MinNodes, placeResourceNodesParams.MaxNodes + 1);
                var room = rooms[i];
                for (int n = 0; n < nodeCount; n++)
                {
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
                        int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
                        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                        var tile = result.Chunk.Tiles[x, y];
                        if (tile.Type != TileType.Floor || tile.HasPlaceable) continue;
                        if (result.HasAnythingAt(worldOffset.Offset(x, y)))
                            continue;

                        var def = GameData.Instance.ResourceNodes.Pick(rng, biome);
                        result.ResourceNodes.Add((worldOffset.Offset(x, y), def));
                        break;
                    }
                }
            }
        }
    }

    public static void CarveHLine(Chunk chunk, int x1, int x2, int y, int floorTileId)
    {
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);
        for (int x = minX; x <= maxX; x++)
            CarveTile(chunk, x, y, floorTileId);
    }

    public static void CarveVLine(Chunk chunk, int y1, int y2, int x, int floorTileId)
    {
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
            CarveTile(chunk, x, y, floorTileId);
    }

    public static void CarveCorridor(Chunk chunk, int x1, int y1, int x2, int y2, SeededRandom rng, int floorTileId)
    {
        if (rng.Next(2) == 0)
        {
            CarveHLine(chunk, x1, x2, y1, floorTileId);
            CarveVLine(chunk, y1, y2, x2, floorTileId);
        }
        else
        {
            CarveVLine(chunk, y1, y2, x1, floorTileId);
            CarveHLine(chunk, x1, x2, y2, floorTileId);
        }
    }

    public static (int X, int Y)? FindSpawnPoint(Chunk chunk)
    {
        int worldOffsetX = chunk.ChunkPosition.X * Chunk.Size;
        int worldOffsetY = chunk.ChunkPosition.Y * Chunk.Size;
        int midX = Chunk.Size / 2;
        int midY = Chunk.Size / 2;
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
                        return (worldOffsetX + cx, worldOffsetY + cy);
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

    public static void MakeTilesFloorInRadius(GenerationResult result, int x, int y, int radius, int floorTileId)
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
                chunk.Tiles[cx, cy].TileId = floorTileId;
            }
        }
    }

    public static List<Room> ExtractRooms(Chunk chunk, int padding, int minRoomArea, int gridStep = 12, int minRooms = 2)
    {
        var map = new bool[Chunk.Size, Chunk.Size];
        var tiles = chunk.Tiles;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (tiles[x, y].Type == TileType.Floor)
                    map[x, y] = true;

        return ExtractRooms(map, Chunk.Size, padding, minRoomArea, gridStep, minRooms);
    }

    /// <summary>
    /// Subdivide the cave's floor area into room-like regions for monster/item placement.
    /// Uses a grid-based sampling to find clusters of open space.
    /// </summary>
    public static List<Room> ExtractRooms(bool[,] map, int size, int padding, int minRoomArea, int gridStep = 12, int minRooms = 2)
    {
        var rooms = new List<Room>();

        for (int gx = padding + 2; gx < size - padding - 6; gx += gridStep)
            for (int gy = padding + 2; gy < size - padding - 6; gy += gridStep)
            {
                // Find the largest open rectangle starting near gx,gy
                int bestX = gx, bestY = gy, bestW = 0, bestH = 0;
                for (int sx = gx; sx < Math.Min(gx + 4, size - 5); sx++)
                    for (int sy = gy; sy < Math.Min(gy + 4, size - 5); sy++)
                    {
                        var (w, h) = MeasureOpenRect(map, sx, sy, size);
                        if (w * h > bestW * bestH)
                        {
                            bestX = sx; bestY = sy; bestW = w; bestH = h;
                        }
                    }

                if (bestW >= 4 && bestH >= 4 && bestW * bestH >= minRoomArea)
                    rooms.Add(new Room(bestX, bestY, bestW, bestH));
            }

        if (rooms.Count < minRooms)
        {
            // Fallback: scan for any open areas
            for (int x = 4; x < size - 8 && rooms.Count < minRooms; x += 8)
            {
                for (int y = 4; y < size - 8 && rooms.Count < minRooms; y += 8)
                {
                    if (map[x, y] && map[x + 1, y] && map[x, y + 1] && map[x + 1, y + 1])
                        rooms.Add(new Room(x, y, 4, 4));
                }
            }
        }

        return rooms;
    }

    private static (int w, int h) MeasureOpenRect(bool[,] map, int sx, int sy, int size)
    {
        int maxW = Math.Min(12, size - sx);
        int maxH = Math.Min(12, size - sy);
        int w = 0, h = 0;

        // Find max width of the first row
        for (int x = sx; x < sx + maxW; x++)
        {
            if (!map[x, sy]) break;
            w++;
        }
        if (w < 4) return (0, 0);

        // Extend height while the full width remains open
        for (int y = sy; y < sy + maxH; y++)
        {
            bool rowOpen = true;
            for (int x = sx; x < sx + w; x++)
            {
                if (!map[x, y]) { rowOpen = false; break; }
            }
            if (!rowOpen) break;
            h++;
        }

        return (w, h);
    }
}

/// <summary>Axis-aligned room within a chunk.</summary>
public class Room
{
    public int X, Y, Width, Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public Room(int x, int y, int w, int h)
    {
        X = x; Y = y; Width = w; Height = h;
    }

    public bool Overlaps(Room other)
    {
        return X < other.X + other.Width && X + Width > other.X &&
               Y < other.Y + other.Height && Y + Height > other.Y;
    }
}
