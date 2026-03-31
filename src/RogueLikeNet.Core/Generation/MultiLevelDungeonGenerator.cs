using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates dungeon levels across multiple Z layers with stairs connecting them.
/// The overworld (Z = 127) gets a standard BSP dungeon with a down-stair entrance.
/// Levels below the overworld (Z &lt; 127) get progressively harder dungeons.
/// Levels above the overworld (Z &gt; 127) return empty chunks.
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

    public GenerationResult Generate(int chunkX, int chunkY, int chunkZ)
    {
        var chunk = new Chunk(chunkX, chunkY, chunkZ);
        var result = new GenerationResult(chunk);

        // Only generate below or at the overworld level
        if (chunkZ > Position.DefaultZ)
            return result;

        // Unique seed per chunk position including Z
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678) + ((long)chunkZ * 0x9E3779B9));
        var rng = new SeededRandom(chunkSeed);

        int depth = Position.DefaultZ - chunkZ; // 0 at surface, increases going down
        var biome = BiomeDefinitions.GetBiomeForChunk(chunkX, chunkY, _seed);

        DungeonHelper.FillWalls(chunk);

        // Build BSP tree
        var root = new BspNode(Padding, Padding, Chunk.Size - Padding * 2, Chunk.Size - Padding * 2);
        SplitNode(root, rng);

        var rooms = new List<Room>();
        CreateRooms(root, rng, rooms);

        foreach (var room in rooms)
            DungeonHelper.CarveRoom(chunk, room);

        ConnectRooms(root, chunk, rng);

        // Place stairs: up-stairs always (to return to the level above),
        // down-stairs to go deeper (always available)
        if (rooms.Count >= 2)
        {
            var first = rooms[0];
            var last = rooms[^1];

            // Up-stairs in first room (connects to level above)
            DungeonHelper.PlaceFeature(chunk, first.CenterX, first.CenterY, TileType.StairsUp,
                TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);

            // Down-stairs in last room (connects to level below)
            if (chunkZ > 0) // Don't place down-stairs at the deepest possible level
            {
                DungeonHelper.PlaceFeature(chunk, last.CenterX, last.CenterY, TileType.StairsDown,
                    TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
            }
        }
        else if (rooms.Count == 1)
        {
            var room = rooms[0];
            DungeonHelper.PlaceFeature(chunk, room.X + 1, room.Y + 1, TileType.StairsUp,
                TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);
            if (chunkZ > 0)
            {
                DungeonHelper.PlaceFeature(chunk, room.X + room.Width - 2, room.Y + room.Height - 2,
                    TileType.StairsDown, TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
            }
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
            result.SpawnPosition = (worldOffsetX + rooms[0].CenterX, worldOffsetY + rooms[0].CenterY, chunkZ);

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
}
