using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates dungeon levels across multiple Z layers with stairs connecting them.
/// The overworld (Z = 127) gets a standard BSP dungeon with a down-stair entrance.
/// Levels below the overworld (Z &lt; 127) get progressively harder dungeons.
/// Levels above the overworld (Z &gt; 127) return empty chunks.
/// Stair positions are deterministic per Z-boundary so up/down stairs always align.
/// </summary>
public class MultiLevelDungeonGenerator : IDungeonGenerator
{
    private const int MinRoomSize = 5;
    private const int MaxRoomSize = 15;
    private const int MinLeafSize = 8;
    private const int Padding = 1;

    private readonly long _seed;

    public MultiLevelDungeonGenerator(long seed)
    {
        _seed = seed;
    }

    /// <summary>
    /// Computes a deterministic stair position within a chunk for the boundary between
    /// <paramref name="lowerZ"/> and <paramref name="lowerZ"/>+1. Both levels will
    /// use this same local coordinate, guaranteeing bidirectional stairs.
    /// </summary>
    private (int LocalX, int LocalY) GetStairPosition(int chunkX, int chunkY, int lowerZ)
    {
        long stairSeed = _seed ^ (((long)chunkX * 0x6C078965) + ((long)chunkY * 0x5D588B65) + ((long)lowerZ * 0x3C6EF35F));
        var stairRng = new SeededRandom(stairSeed);
        // Place within interior of chunk (avoid edges)
        int margin = 3;
        int x = margin + stairRng.Next(Chunk.Size - margin * 2);
        int y = margin + stairRng.Next(Chunk.Size - margin * 2);
        return (x, y);
    }

    public bool Exists(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        return chunkZ <= Position.DefaultZ;
    }

    public GenerationResult Generate(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        // Only generate below or at the overworld level
        if (chunkZ > Position.DefaultZ)
            return result;

        // Unique seed per chunk position including Z
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678) + ((long)chunkZ * 0x9E3779B9));
        var rng = new SeededRandom(chunkSeed);

        int depth = Position.DefaultZ - chunkZ; // 0 at surface, increases going down
        var biome = BiomeDefinitions.GetBiomeForChunk(chunkPos, _seed);

        DungeonHelper.FillWalls(chunk);

        // Build BSP tree
        var root = new BspNode(Padding, Padding, Chunk.Size - Padding * 2, Chunk.Size - Padding * 2);
        SplitNode(root, rng);

        var rooms = new List<Room>();
        CreateRooms(root, rng, rooms);

        foreach (var room in rooms)
            DungeonHelper.CarveRoom(chunk, room);

        ConnectRooms(root, chunk, rng);

        // Place stairs using deterministic boundary positions
        // Up-stairs: connects this level to the level above (boundary between chunkZ and chunkZ+1)
        if (chunkZ < Position.DefaultZ)
        {
            var (upX, upY) = GetStairPosition(chunkX, chunkY, chunkZ);
            DungeonHelper.CarveFloor(chunk, upX, upY);
            DungeonHelper.PlaceFeature(chunk, upX, upY, TileType.StairsUp,
                TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);
            EnsureConnected(chunk, rooms, upX, upY, rng);
        }

        // Down-stairs: connects this level to the level below (boundary between chunkZ-1 and chunkZ)
        if (chunkZ > 0)
        {
            var (downX, downY) = GetStairPosition(chunkX, chunkY, chunkZ - 1);
            DungeonHelper.CarveFloor(chunk, downX, downY);
            DungeonHelper.PlaceFeature(chunk, downX, downY, TileType.StairsDown,
                TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
            EnsureConnected(chunk, rooms, downX, downY, rng);
        }

        // At overworld level, place a down-stair to go below
        if (chunkZ == Position.DefaultZ)
        {
            var (downX, downY) = GetStairPosition(chunkX, chunkY, chunkZ - 1);
            DungeonHelper.CarveFloor(chunk, downX, downY);
            DungeonHelper.PlaceFeature(chunk, downX, downY, TileType.StairsDown,
                TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
            EnsureConnected(chunk, rooms, downX, downY, rng);
        }

        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);
        DungeonHelper.PlaceDecorations(chunk, biome, rng);

        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY)) + depth;
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.ApplyBiomeTint(chunk, biome);

        // Spawn point at overworld level only
        if (chunkX == 0 && chunkY == 0 && chunkZ == Position.DefaultZ && rooms.Count > 0)
            result.SpawnPosition = Position.FromCoords(worldOffsetX + rooms[0].CenterX, worldOffsetY + rooms[0].CenterY, chunkZ);

        return result;
    }

    private static void SplitNode(BspNode node, SeededRandom rng)
    {
        if (node.Width < MinLeafSize * 2 && node.Height < MinLeafSize * 2)
            return;

        bool splitHorizontal;
        if (node.Width < MinLeafSize * 2) splitHorizontal = true;
        else if (node.Height < MinLeafSize * 2) splitHorizontal = false;
        else splitHorizontal = rng.NextBool();

        if (splitHorizontal)
        {
            int split = rng.Next(MinLeafSize, node.Height - MinLeafSize);
            node.Left = new BspNode(node.X, node.Y, node.Width, split);
            node.Right = new BspNode(node.X, node.Y + split, node.Width, node.Height - split);
        }
        else
        {
            int split = rng.Next(MinLeafSize, node.Width - MinLeafSize);
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

        int w = rng.Next(MinRoomSize, Math.Min(MaxRoomSize, node.Width - 2));
        int h = rng.Next(MinRoomSize, Math.Min(MaxRoomSize, node.Height - 2));
        int x = rng.Next(node.X + 1, node.X + node.Width - w - 1);
        int y = rng.Next(node.Y + 1, node.Y + node.Height - h - 1);
        var room = new Room(x, y, w, h);
        node.Room = room;
        rooms.Add(room);
    }

    private static void ConnectRooms(BspNode node, Chunk chunk, SeededRandom rng)
    {
        if (node.Left == null || node.Right == null) return;
        ConnectRooms(node.Left, chunk, rng);
        ConnectRooms(node.Right, chunk, rng);

        var leftRoom = GetRoom(node.Left);
        var rightRoom = GetRoom(node.Right);
        if (leftRoom == null || rightRoom == null) return;
        DungeonHelper.CarveCorridor(chunk, leftRoom.CenterX, leftRoom.CenterY, rightRoom.CenterX, rightRoom.CenterY, rng);
    }

    private static Room? GetRoom(BspNode node)
    {
        if (node.Room != null) return node.Room;
        var left = node.Left != null ? GetRoom(node.Left) : null;
        return left ?? (node.Right != null ? GetRoom(node.Right) : null);
    }

    /// <summary>
    /// Ensures the stair tile at (sx, sy) is connected to the nearest room by carving a corridor.
    /// </summary>
    private static void EnsureConnected(Chunk chunk, List<Room> rooms, int sx, int sy, SeededRandom rng)
    {
        if (rooms.Count == 0) return;

        // Find nearest room center
        int bestDist = int.MaxValue;
        Room? best = null;
        foreach (var room in rooms)
        {
            int dist = Math.Abs(room.CenterX - sx) + Math.Abs(room.CenterY - sy);
            if (dist < bestDist) { bestDist = dist; best = room; }
        }

        if (best != null)
            DungeonHelper.CarveCorridor(chunk, sx, sy, best.CenterX, best.CenterY, rng);
    }
}
