using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a winding directional tunnel with chambers branching off.
/// Based on the "Basic Directional Dungeon Generation" algorithm from RogueBasin.
/// Used for linear biomes: Ice (frozen passages), Sewer (tunnel networks).
/// </summary>
public class DirectionalTunnelGenerator : IDungeonGenerator
{
    private const int Padding = 1;
    private const int MinTunnelWidth = 3;
    private const int MaxTunnelWidth = 7;
    private const int ChamberChance = 25; // % per row of spawning a side chamber
    private const int MinChamberSize = 4;
    private const int MaxChamberSize = 8;

    private readonly long _seed;

    public DirectionalTunnelGenerator(long seed)
    {
        _seed = seed;
    }

    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var rng = new SeededRandom(chunkSeed);
        var biome = BiomeRegistry.GetBiomeForChunk(chunkPos, _seed);
        int wallTileId = GameData.Instance.Biomes.GetWallTileId(biome);
        int floorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);

        var rooms = GenerateLayout(chunk, rng, wallTileId, floorTileId);

        DungeonHelper.PlaceStairs(chunk, rooms);
        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);
        DungeonHelper.PlaceDecorations(chunk, biome, rng, result);
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));
        var worldOffset = chunk.LocalToWorld(0, 0);
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffset);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffset);

        // Spawn point: center of the first room/chamber
        if (chunkX == 0 && chunkY == 0 && rooms.Count > 0)
            result.SpawnPosition = worldOffset.Offset(rooms[0].CenterX, rooms[0].CenterY);

        return result;
    }

    public static List<Room> GenerateLayout(Chunk chunk, SeededRandom rng, int wallTileId, int floorTileId,
        int entranceLx = -1, int entranceLy = -1)
    {
        DungeonHelper.FillWalls(chunk, wallTileId);

        var rooms = new List<Room>();

        // Generate vertical tunnel (top to bottom)
        CarveVerticalTunnel(chunk, rng, rooms, floorTileId);
        // Generate horizontal tunnel (left to right)
        CarveHorizontalTunnel(chunk, rng, rooms, floorTileId);

        // Remove any rooms that are completely overlapped by another (can happen due the the two tunnels crossing and chambers spawning on top of them)
        RemoveOverlappingRooms(rooms, rng);

        // Connect entrance to nearest room if provided
        if (entranceLx >= 0 && entranceLy >= 0 && rooms.Count > 0)
        {
            DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy, floorTileId);
            var nearest = rooms.OrderBy(r =>
                Math.Abs(r.CenterX - entranceLx) + Math.Abs(r.CenterY - entranceLy)).First();
            DungeonHelper.CarveCorridor(chunk, entranceLx, entranceLy,
                nearest.CenterX, nearest.CenterY, rng, floorTileId);
        }

        return rooms;
    }

    private static void RemoveOverlappingRooms(List<Room> rooms, SeededRandom rng)
    {
        // Shuffle rooms to avoid bias towards removing later ones in the list
        rooms = rooms.OrderBy(_ => rng.Next()).ToList();

        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (rooms[i].Overlaps(rooms[j]))
                {
                    rooms.RemoveAt(j);
                    j--; // Check the new room at index j
                }
            }
        }
    }

    private static void CarveHorizontalTunnel(Chunk chunk, SeededRandom rng, List<Room> rooms, int floorTileId)
    {
        int tunnelWidth = MinTunnelWidth + rng.Next(MaxTunnelWidth - MinTunnelWidth + 1);
        int roughness = 40 + rng.Next(30);
        int windyness = 30 + rng.Next(40);

        var size = Chunk.Size;

        // Tunnel runs left to right
        int posY = size / 4 + rng.Next(size / 2);

        for (int x = Padding; x < size - Padding; x++)
        {
            // Roughness: vary width
            if (rng.Next(100) < roughness)
            {
                int delta = rng.Next(3) - 1; // -1, 0, or 1
                tunnelWidth = Math.Clamp(tunnelWidth + delta, MinTunnelWidth, MaxTunnelWidth);
            }

            // Windyness: vary position
            if (rng.Next(100) < windyness)
            {
                int delta = rng.Next(3) - 1;
                posY = Math.Clamp(posY + delta, Padding + tunnelWidth / 2 + 1, size - Padding - tunnelWidth / 2 - 1);
            }

            // Carve tunnel cross-section
            int halfW = tunnelWidth / 2;
            for (int dy = -halfW; dy <= halfW; dy++)
                DungeonHelper.CarveTile(chunk, x, posY + dy, floorTileId);

            // Chamber chance
            if (rng.Next(100) < ChamberChance && x > Padding + 3 && x < size - Padding - MaxChamberSize)
            {
                int chamberW = MinChamberSize + rng.Next(MaxChamberSize - MinChamberSize + 1);
                int chamberH = MinChamberSize + rng.Next(MaxChamberSize - MinChamberSize + 1);
                bool above = rng.Next(2) == 0;
                int chamberY = above
                    ? posY - halfW - chamberH
                    : posY + halfW + 1;

                if (chamberY >= Padding && chamberY + chamberH < size - Padding)
                {
                    var room = new Room(x, chamberY, chamberW, chamberH);
                    DungeonHelper.CarveRoom(chunk, room, floorTileId);
                    rooms.Add(room);
                }
            }
        }
    }

    private static void CarveVerticalTunnel(Chunk chunk, SeededRandom rng, List<Room> rooms, int floorTileId)
    {
        int tunnelWidth = MinTunnelWidth + rng.Next(MaxTunnelWidth - MinTunnelWidth + 1);
        int roughness = 40 + rng.Next(30);
        int windyness = 30 + rng.Next(40);

        var size = Chunk.Size;

        // Tunnel runs top to bottom
        int posX = size / 4 + rng.Next(size / 2);

        for (int y = Padding; y < size - Padding; y++)
        {
            if (rng.Next(100) < roughness)
            {
                int delta = rng.Next(3) - 1;
                tunnelWidth = Math.Clamp(tunnelWidth + delta, MinTunnelWidth, MaxTunnelWidth);
            }

            if (rng.Next(100) < windyness)
            {
                int delta = rng.Next(3) - 1;
                posX = Math.Clamp(posX + delta, Padding + tunnelWidth / 2 + 1, size - Padding - tunnelWidth / 2 - 1);
            }

            int halfW = tunnelWidth / 2;
            for (int dx = -halfW; dx <= halfW; dx++)
                DungeonHelper.CarveTile(chunk, posX + dx, y, floorTileId);

            if (rng.Next(100) < ChamberChance && y > Padding + 3 && y < size - Padding - MaxChamberSize)
            {
                int chamberW = MinChamberSize + rng.Next(MaxChamberSize - MinChamberSize + 1);
                int chamberH = MinChamberSize + rng.Next(MaxChamberSize - MinChamberSize + 1);
                bool left = rng.Next(2) == 0;
                int chamberX = left
                    ? posX - halfW - chamberW
                    : posX + halfW + 1;

                if (chamberX >= Padding && chamberX + chamberW < size - Padding)
                {
                    var room = new Room(chamberX, y, chamberW, chamberH);
                    DungeonHelper.CarveRoom(chunk, room, floorTileId);
                    rooms.Add(room);
                }
            }
        }
    }
}
