using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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

        // Spawn point: center of the first extracted room
        if (chunkX == 0 && chunkY == 0 && rooms.Count > 0)
            result.SpawnPosition = worldOffset.Offset(rooms[0].CenterX, rooms[0].CenterY);

        return result;
    }

    public static List<Room> GenerateLayout(Chunk chunk, SeededRandom rng, int wallTileId, int floorTileId,
        int entranceLx = -1, int entranceLy = -1)
    {
        int size = Chunk.Size;

        // Step 1: Fill with walls
        DungeonHelper.FillWalls(chunk, wallTileId);

        // Step 2: Randomly carve interior cells
        var map = new bool[size, size]; // true = floor
        for (int x = Padding; x < size - Padding; x++)
            for (int y = Padding; y < size - Padding; y++)
                map[x, y] = rng.Next(100) >= WallFillPercent;

        // Force entrance tile open before smoothing so the cave grows around it
        bool hasEntrance = entranceLx >= 0 && entranceLy >= 0;
        if (hasEntrance)
            map[entranceLx, entranceLy] = true;

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
            // Keep entrance open through smoothing
            if (hasEntrance)
                next[entranceLx, entranceLy] = true;
            map = next;
        }

        // Step 4: Apply cave map to chunk tiles
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y])
                    DungeonHelper.CarveFloor(chunk, x, y, floorTileId);
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

        // If entrance exists, prefer the region containing it; otherwise keep the largest
        int keepId;
        if (hasEntrance && regionMap[entranceLx, entranceLy] != 0)
            keepId = regionMap[entranceLx, entranceLy];
        else
        {
            keepId = 0;
            int keepSize = 0;
            foreach (var (id, sz) in regionSizes)
            {
                if (sz > keepSize) { keepId = id; keepSize = sz; }
            }
        }

        // Wall off disconnected regions
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (map[x, y] && regionMap[x, y] != keepId)
                {
                    map[x, y] = false;
                    chunk.Tiles[x, y].TileId = wallTileId;
                }
            }

        // Step 6: Extract room-like areas for population, ensure at least 2 for stairs
        var rooms = DungeonHelper.ExtractRooms(map, size, Padding, MinRoomArea, minRooms: 2);

        // Connect entrance to nearest room if provided and not already inside one
        if (hasEntrance && rooms.Count > 0)
        {
            DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy, floorTileId);
            var nearest = rooms.OrderBy(r =>
                Math.Abs(r.CenterX - entranceLx) + Math.Abs(r.CenterY - entranceLy)).First();
            DungeonHelper.CarveCorridor(chunk, entranceLx, entranceLy,
                nearest.CenterX, nearest.CenterY, rng, floorTileId);
        }

        return rooms;
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


}
