using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// BSP (Binary Space Partition) dungeon generator.
/// All arithmetic is integer-only. Produces rooms connected by corridors.
/// </summary>
public class BspDungeonGenerator : IDungeonGenerator
{
    private const int MinRoomSize = 5;
    private const int MaxRoomSize = 15;
    private const int MinLeafSize = 8;
    private const int Padding = 1;

    public GenerationResult Generate(Chunk chunk, long seed)
    {
        var rng = new SeededRandom(seed);
        var result = new GenerationResult();
        int width = Chunk.Size;
        int height = Chunk.Size;
        var biome = BiomeDefinitions.GetBiomeForChunk(chunk.ChunkX, chunk.ChunkY, seed);

        // Fill with walls
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            ref var tile = ref chunk.Tiles[x, y];
            tile.Type = TileType.Wall;
            tile.GlyphId = TileDefinitions.GlyphWall;
            tile.FgColor = TileDefinitions.ColorWallFg;
            tile.BgColor = TileDefinitions.ColorBlack;
        }

        // Build BSP tree
        var root = new BspNode(Padding, Padding, width - Padding * 2, height - Padding * 2);
        SplitNode(root, rng);

        // Create rooms in leaf nodes
        var rooms = new List<Room>();
        CreateRooms(root, rng, rooms);

        // Carve rooms into chunk
        foreach (var room in rooms)
            CarveRoom(chunk, room);

        // Connect rooms via BSP siblings
        ConnectRooms(root, chunk, rng);

        // Place stairs
        if (rooms.Count >= 2)
        {
            var first = rooms[0];
            var last = rooms[^1];
            PlaceFeature(chunk, first.CenterX, first.CenterY, TileType.StairsUp, TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);
            PlaceFeature(chunk, last.CenterX, last.CenterY, TileType.StairsDown, TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
        }

        // Biome-specific: liquid pools in select rooms
        PlaceLiquidPools(chunk, rooms, biome, rng);

        // Biome-specific: scatter decorations on floor tiles
        PlaceDecorations(chunk, biome, rng);

        // Populate rooms with monsters, items, and torches (skip first room — spawn area)
        for (int i = 1; i < rooms.Count; i++)
            PopulateRoom(rooms[i], rng, result);

        // Apply biome tint to all tile colors
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            ref var tile = ref chunk.Tiles[x, y];
            tile.FgColor = BiomeDefinitions.ApplyBiomeTint(tile.FgColor, biome);
            tile.BgColor = BiomeDefinitions.ApplyBiomeTint(tile.BgColor, biome);
        }

        return result;
    }

    private static void PlaceLiquidPools(Chunk chunk, List<Room> rooms, BiomeType biome, SeededRandom rng)
    {
        var liquidDef = BiomeDefinitions.GetLiquid(biome);
        if (liquidDef == null) return;
        var liq = liquidDef.Value;

        // Skip the first room (spawn area) and last room (stairs)
        for (int i = 1; i < rooms.Count - 1; i++)
        {
            if (rng.Next(100) >= liq.RoomChance) continue;
            var room = rooms[i];
            if (room.Width < 6 || room.Height < 6) continue;

            // Carve a small pool in the room interior (leaving 2-tile walkable border)
            int poolX = room.X + 2;
            int poolY = room.Y + 2;
            int poolW = room.Width - 4;
            int poolH = room.Height - 4;

            for (int x = poolX; x < poolX + poolW; x++)
            for (int y = poolY; y < poolY + poolH; y++)
            {
                if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
                {
                    ref var tile = ref chunk.Tiles[x, y];
                    tile.Type = liq.Type;
                    tile.GlyphId = liq.GlyphId;
                    tile.FgColor = liq.FgColor;
                    tile.BgColor = liq.BgColor;
                }
            }
        }
    }

    private static void PlaceDecorations(Chunk chunk, BiomeType biome, SeededRandom rng)
    {
        var decorations = BiomeDefinitions.GetDecorations(biome);
        if (decorations.Length == 0) return;

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            ref var tile = ref chunk.Tiles[x, y];
            if (tile.Type != TileType.Floor) continue;

            foreach (var deco in decorations)
            {
                if (rng.Next(100) < deco.Chance)
                {
                    tile.Type = TileType.Decoration;
                    tile.GlyphId = deco.GlyphId;
                    tile.FgColor = deco.FgColor;
                    break; // Only one decoration per tile
                }
            }
        }
    }

    private static void PopulateRoom(Room room, SeededRandom rng, GenerationResult result)
    {
        // 1-3 monsters per room
        int monsterCount = 1 + rng.Next(3);
        for (int m = 0; m < monsterCount; m++)
        {
            int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
            int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
            result.SpawnPoints.Add(new SpawnPoint(x, y, SpawnType.Monster));
        }

        // 30% chance of an item
        if (rng.Next(100) < 30)
        {
            int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
            int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
            result.SpawnPoints.Add(new SpawnPoint(x, y, SpawnType.Item));
        }

        // 40% chance of a torch (light source)
        if (rng.Next(100) < 40)
        {
            result.SpawnPoints.Add(new SpawnPoint(room.CenterX, room.CenterY, SpawnType.Torch));
        }
    }

    private static void SplitNode(BspNode node, SeededRandom rng)
    {
        if (node.Width < MinLeafSize * 2 && node.Height < MinLeafSize * 2)
            return;

        bool splitHorizontal;
        if (node.Width < MinLeafSize * 2) splitHorizontal = true;
        else if (node.Height < MinLeafSize * 2) splitHorizontal = false;
        else splitHorizontal = rng.Next(2) == 0;

        if (splitHorizontal)
        {
            if (node.Height < MinLeafSize * 2) return;
            int split = MinLeafSize + rng.Next(node.Height - MinLeafSize * 2 + 1);
            node.Left = new BspNode(node.X, node.Y, node.Width, split);
            node.Right = new BspNode(node.X, node.Y + split, node.Width, node.Height - split);
        }
        else
        {
            if (node.Width < MinLeafSize * 2) return;
            int split = MinLeafSize + rng.Next(node.Width - MinLeafSize * 2 + 1);
            node.Left = new BspNode(node.X, node.Y, split, node.Height);
            node.Right = new BspNode(node.X + split, node.Y, node.Width - split, node.Height);
        }

        SplitNode(node.Left, rng);
        SplitNode(node.Right, rng);
    }

    private static void CreateRooms(BspNode node, SeededRandom rng, List<Room> rooms)
    {
        if (node.Left != null && node.Right != null)
        {
            CreateRooms(node.Left, rng, rooms);
            CreateRooms(node.Right, rng, rooms);
            return;
        }

        // Leaf node — create a room
        int roomW = MinRoomSize + rng.Next(Math.Min(MaxRoomSize, node.Width - 2) - MinRoomSize + 1);
        int roomH = MinRoomSize + rng.Next(Math.Min(MaxRoomSize, node.Height - 2) - MinRoomSize + 1);
        int roomX = node.X + 1 + rng.Next(node.Width - roomW - 2 + 1);
        int roomY = node.Y + 1 + rng.Next(node.Height - roomH - 2 + 1);

        var room = new Room(roomX, roomY, roomW, roomH);
        node.Room = room;
        rooms.Add(room);
    }

    private static void CarveRoom(Chunk chunk, Room room)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
        for (int y = room.Y; y < room.Y + room.Height; y++)
        {
            if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
        }
    }

    private static void ConnectRooms(BspNode node, Chunk chunk, SeededRandom rng)
    {
        if (node.Left == null || node.Right == null) return;

        ConnectRooms(node.Left, chunk, rng);
        ConnectRooms(node.Right, chunk, rng);

        var leftRoom = GetRoom(node.Left, rng);
        var rightRoom = GetRoom(node.Right, rng);
        if (leftRoom == null || rightRoom == null) return;

        CarveCorridor(chunk, leftRoom.CenterX, leftRoom.CenterY, rightRoom.CenterX, rightRoom.CenterY, rng);
    }

    private static Room? GetRoom(BspNode node, SeededRandom rng)
    {
        if (node.Room != null) return node.Room;
        if (node.Left == null && node.Right == null) return null;

        var leftRoom = node.Left != null ? GetRoom(node.Left, rng) : null;
        var rightRoom = node.Right != null ? GetRoom(node.Right, rng) : null;

        if (leftRoom == null) return rightRoom;
        if (rightRoom == null) return leftRoom;
        return rng.Next(2) == 0 ? leftRoom : rightRoom;
    }

    private static void CarveCorridor(Chunk chunk, int x1, int y1, int x2, int y2, SeededRandom rng)
    {
        // L-shaped corridor: go horizontal first, then vertical (or vice versa)
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

    private static void CarveHLine(Chunk chunk, int x1, int x2, int y)
    {
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);
        for (int x = minX; x <= maxX; x++)
            CarveTile(chunk, x, y);
    }

    private static void CarveVLine(Chunk chunk, int y1, int y2, int x)
    {
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
            CarveTile(chunk, x, y);
    }

    private static void CarveTile(Chunk chunk, int x, int y)
    {
        if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
        {
            ref var tile = ref chunk.Tiles[x, y];
            if (tile.Type == TileType.Wall)
            {
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
        }
    }

    private static void PlaceFeature(Chunk chunk, int x, int y, TileType type, int glyph, int fgColor)
    {
        if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
        {
            ref var tile = ref chunk.Tiles[x, y];
            tile.Type = type;
            tile.GlyphId = glyph;
            tile.FgColor = fgColor;
        }
    }
}

internal class BspNode
{
    public int X, Y, Width, Height;
    public BspNode? Left, Right;
    public Room? Room;

    public BspNode(int x, int y, int w, int h)
    {
        X = x; Y = y; Width = w; Height = h;
    }
}

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
