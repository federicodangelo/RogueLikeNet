using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// BSP (Binary Space Partition) dungeon generator.
/// All arithmetic is integer-only. Produces rooms connected by corridors.
/// Used for structured biomes: Stone, Arcane, Crypt, Ruined.
/// </summary>
public class BspDungeonGenerator : IDungeonGenerator
{
    private const int MinRoomSize = 5;
    private const int MaxRoomSize = 15;
    private const int MinLeafSize = 8;
    private const int Padding = 1;

    private readonly long _seed;

    public BspDungeonGenerator(long seed)
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

        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var rng = new SeededRandom(chunkSeed);
        int size = Chunk.Size;
        var biome = BiomeDefinitions.GetBiomeForChunk(chunkPos, _seed);

        DungeonHelper.FillWalls(chunk);

        // Build BSP tree
        var root = new BspNode(Padding, Padding, size - Padding * 2, size - Padding * 2);
        SplitNode(root, rng);

        // Create rooms in leaf nodes
        var rooms = new List<Room>();
        CreateRooms(root, rng, rooms);

        // Carve rooms into chunk
        foreach (var room in rooms)
            DungeonHelper.CarveRoom(chunk, room);

        // Connect rooms via BSP siblings
        ConnectRooms(root, chunk, rng);

        DungeonHelper.PlaceStairs(chunk, rooms);
        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);
        DungeonHelper.PlaceDecorations(chunk, biome, rng);
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.ApplyBiomeTint(chunk, biome);

        // Spawn point: center of the first room (before any monsters are placed there)
        if (chunkX == 0 && chunkY == 0 && rooms.Count > 0)
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

    private static void ConnectRooms(BspNode node, Chunk chunk, SeededRandom rng)
    {
        if (node.Left == null || node.Right == null) return;

        ConnectRooms(node.Left, chunk, rng);
        ConnectRooms(node.Right, chunk, rng);

        var leftRoom = GetRoom(node.Left, rng);
        var rightRoom = GetRoom(node.Right, rng);
        if (leftRoom == null || rightRoom == null) return;

        DungeonHelper.CarveCorridor(chunk, leftRoom.CenterX, leftRoom.CenterY,
            rightRoom.CenterX, rightRoom.CenterY, rng);
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
