using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates organic cave-like dungeons using cellular automata.
/// Fills with random walls (~45%), then runs smoothing passes.
/// Used for natural/chaotic biomes: Lava, Forest, Fungal, Infernal.
/// </summary>
public class CellularAutomataCaveGenerator : IDungeonGenerator
{
    private const int Padding = 1;
    private const int SmoothPasses = 5;
    private const int WallFillPercent = 45;
    private const int MinRoomArea = 25;

    private readonly long _seed;

    public CellularAutomataCaveGenerator(long seed)
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
        int size = Chunk.Size;
        var biome = BiomeRegistry.GetBiomeForChunk(chunkPos, _seed);

        // Step 1: Fill with walls
        DungeonHelper.FillWalls(chunk);

        // Step 2: Randomly carve interior cells
        var map = new bool[size, size]; // true = floor
        for (int x = Padding; x < size - Padding; x++)
            for (int y = Padding; y < size - Padding; y++)
                map[x, y] = rng.Next(100) >= WallFillPercent;

        // Step 3: Cellular automata smoothing
        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            var next = new bool[size, size];
            for (int x = Padding; x < size - Padding; x++)
                for (int y = Padding; y < size - Padding; y++)
                {
                    int walls = CountWallNeighbors(map, x, y, size);
                    next[x, y] = walls < 5; // B5678/S45678 rule
                }
            map = next;
        }

        // Step 4: Apply cave map to chunk tiles
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y])
                    DungeonHelper.CarveFloor(chunk, x, y);
            }

        // Step 5: Flood fill to find connected regions, keep largest
        var regionMap = new int[size, size];
        var regionSizes = new Dictionary<int, int>();
        int regionId = 0;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y] && regionMap[x, y] == 0)
                {
                    regionId++;
                    int count = FloodFill(map, regionMap, x, y, regionId, size);
                    regionSizes[regionId] = count;
                }
            }

        // Find the largest region
        int largestId = 0;
        int largestSize = 0;
        foreach (var (id, sz) in regionSizes)
        {
            if (sz > largestSize) { largestId = id; largestSize = sz; }
        }

        // Wall off disconnected small regions
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y] && regionMap[x, y] != largestId)
                {
                    map[x, y] = false;
                    ref var tile = ref chunk.Tiles[x, y];
                    tile.Type = TileType.Blocked;
                    tile.GlyphId = TileDefinitions.GlyphWall;
                    tile.FgColor = TileDefinitions.ColorWallFg;
                    tile.BgColor = TileDefinitions.ColorBlack;
                }
            }

        // Step 6: Extract room-like areas for population
        var rooms = ExtractRooms(map, size, rng);

        // Step 7: Place stairs
        DungeonHelper.PlaceStairs(chunk, rooms);

        // Step 8: Liquid pools
        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);

        // Step 9: Decorations
        DungeonHelper.PlaceDecorations(chunk, biome, rng);

        // Step 10: Populate rooms
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffsetX, worldOffsetY, chunkZ);

        // Step 11: Biome tint
        DungeonHelper.ApplyBiomeTint(chunk, biome);

        // Spawn point: center of the first extracted room
        if (chunkX == 0 && chunkY == 0 && rooms.Count > 0)
            result.SpawnPosition = Position.FromCoords(worldOffsetX + rooms[0].CenterX, worldOffsetY + rooms[0].CenterY, chunkZ);

        return result;
    }

    private static int CountWallNeighbors(bool[,] map, int cx, int cy, int size)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= size || !map[nx, ny])
                    count++;
            }
        return count;
    }

    private static int FloodFill(bool[,] map, int[,] regionMap, int startX, int startY, int id, int size)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));
        regionMap[startX, startY] = id;
        int count = 0;

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            count++;
            foreach (var (nx, ny) in Neighbors4(x, y))
            {
                if (nx >= 0 && nx < size && ny >= 0 && ny < size &&
                    map[nx, ny] && regionMap[nx, ny] == 0)
                {
                    regionMap[nx, ny] = id;
                    stack.Push((nx, ny));
                }
            }
        }
        return count;
    }

    private static (int, int)[] Neighbors4(int x, int y) =>
        [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)];

    /// <summary>
    /// Subdivide the cave's floor area into room-like regions for monster/item placement.
    /// Uses a grid-based sampling to find clusters of open space.
    /// </summary>
    private static List<Room> ExtractRooms(bool[,] map, int size, SeededRandom rng)
    {
        var rooms = new List<Room>();
        int gridStep = 12;

        for (int gx = Padding + 2; gx < size - Padding - 6; gx += gridStep)
            for (int gy = Padding + 2; gy < size - Padding - 6; gy += gridStep)
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

                if (bestW >= 4 && bestH >= 4 && bestW * bestH >= MinRoomArea)
                    rooms.Add(new Room(bestX, bestY, bestW, bestH));
            }

        // Ensure at least 2 rooms for stairs
        if (rooms.Count < 2)
        {
            // Fallback: scan for any open areas
            for (int x = 4; x < size - 8 && rooms.Count < 2; x += 8)
                for (int y = 4; y < size - 8 && rooms.Count < 2; y += 8)
                {
                    if (map[x, y] && map[x + 1, y] && map[x, y + 1] && map[x + 1, y + 1])
                        rooms.Add(new Room(x, y, 4, 4));
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
