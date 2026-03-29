using RogueLikeNet.Core.Definitions;
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

    public GenerationResult Generate(int chunkX, int chunkY)
    {
        var chunk = new Chunk(chunkX, chunkY);
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var rng = new SeededRandom(chunkSeed);
        var result = new GenerationResult(chunk);
        int size = Chunk.Size;
        var biome = BiomeDefinitions.GetBiomeForChunk(chunkX, chunkY, _seed);

        // Step 1: Fill with walls
        DungeonHelper.FillWalls(chunk);

        var rooms = new List<Room>();

        // Step 2: Generate 2 directional tunnels for connectivity
        int passes = 2;
        for (int pass = 0; pass < passes; pass++)
        {
            bool horizontal = pass == 0;
            CarveTunnel(chunk, rng, size, horizontal, rooms);
        }

        // Step 3: Place stairs
        DungeonHelper.PlaceStairs(chunk, rooms);

        // Step 4: Liquid pools
        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);

        // Step 5: Decorations
        DungeonHelper.PlaceDecorations(chunk, biome, rng);

        // Step 6: Populate rooms
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffsetX, worldOffsetY);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffsetX, worldOffsetY);

        // Step 7: Biome tint
        DungeonHelper.ApplyBiomeTint(chunk, biome);

        // Spawn point: center of the first room/chamber
        if (chunkX == 0 && chunkY == 0 && rooms.Count > 0)
            result.SpawnPosition = (worldOffsetX + rooms[0].CenterX, worldOffsetY + rooms[0].CenterY);

        return result;
    }

    private static void CarveTunnel(Chunk chunk, SeededRandom rng, int size,
        bool horizontal, List<Room> rooms)
    {
        int tunnelWidth = MinTunnelWidth + rng.Next(MaxTunnelWidth - MinTunnelWidth + 1);
        int roughness = 40 + rng.Next(30);
        int windyness = 30 + rng.Next(40);

        if (horizontal)
        {
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
                    DungeonHelper.CarveTile(chunk, x, posY + dy);

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
                        DungeonHelper.CarveRoom(chunk, room);
                        rooms.Add(room);
                    }
                }
            }

            // Add a start room and end room for stairs placement
            int startRoomW = 5 + rng.Next(3);
            int startRoomH = 5 + rng.Next(3);
            var startRoom = new Room(Padding + 1, posY - startRoomH / 2, startRoomW, startRoomH);
            DungeonHelper.CarveRoom(chunk, startRoom);
            rooms.Insert(0, startRoom);

            int endRoomW = 5 + rng.Next(3);
            int endRoomH = 5 + rng.Next(3);
            var endRoom = new Room(size - Padding - endRoomW - 1, posY - endRoomH / 2, endRoomW, endRoomH);
            DungeonHelper.CarveRoom(chunk, endRoom);
            rooms.Add(endRoom);
        }
        else
        {
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
                    DungeonHelper.CarveTile(chunk, posX + dx, y);

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
                        DungeonHelper.CarveRoom(chunk, room);
                        rooms.Add(room);
                    }
                }
            }
        }
    }
}
