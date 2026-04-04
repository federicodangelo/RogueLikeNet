using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates continuous overworld terrain using layered Perlin noise.
/// Terrain is seamless across chunk boundaries because noise is sampled
/// at world-space coordinates. Biomes are determined by temperature/moisture
/// noise layers, giving gradual transitions at borders.
/// </summary>
public class OverworldGenerator : IDungeonGenerator
{
    // Noise scale: lower = larger features
    private const double TerrainScale = 0.025;
    private const double BiomeScale = 0.012;

    // Terrain threshold: noise > this = floor (lower = more open)
    private const double FloorThreshold = -0.15;

    // Cave detail layer adds small pockets of wall/floor
    private const double DetailScale = 0.10;
    private const double DetailWeight = 0.15;

    // Liquid height threshold: floor tiles with terrain height below this value form liquid pools.
    // FloorThreshold is the minimum height for a floor tile (~-0.15), so the range
    // (FloorThreshold, LiquidHeightThreshold) defines the "low-lying" zone that floods.
    private const double LiquidHeightThreshold = 0.05;

    // Resource node noise layer: clusters resource nodes in natural-looking patches
    private const double ResourceScale = 0.08;
    private const double ResourceRockThreshold = 0.3;
    private const double ResourceTreeThreshold = 0.2;

    // Spawn density: chance per floor tile (out of 1000)
    private const int MonsterChance1000 = 4;
    private const int ItemChance1000 = 1;
    private const int TorchChance1000 = 0; // Disabled for now, it doesn't make sense for overworld

    // Cave entrance noise: determines which chunks have a cave below
    private const double CaveScale = 0.3;
    private const double CaveThreshold = 0.05; // noise > this → cave present

    // BSP parameters for underground caves (matching BspDungeonGenerator)
    private const int BspMinRoomSize = 5;
    private const int BspMaxRoomSize = 15;
    private const int BspMinLeafSize = 8;
    private const int BspPadding = 1;

    private const int CaveZ = Position.DefaultZ - 1;

    private readonly long _seed;

    private readonly PerlinNoise _terrainNoise;
    private readonly PerlinNoise _detailNoise;
    private readonly PerlinNoise _tempNoise;
    private readonly PerlinNoise _moistNoise;
    private readonly PerlinNoise _resourceNoise;
    private readonly PerlinNoise _caveNoise;

    public OverworldGenerator(long seed)
    {
        _seed = seed;

        _terrainNoise = new PerlinNoise(_seed);
        _detailNoise = new PerlinNoise(_seed ^ 0x5DEECE66DL);
        _tempNoise = new PerlinNoise(_seed ^ 0x27BB2EE687B0B0FDL);
        _moistNoise = new PerlinNoise(_seed ^ 0x12345678ABCDEF0L);
        _resourceNoise = new PerlinNoise(_seed ^ 0x3A4F5B6C7D8E9F0AL);
        _caveNoise = new PerlinNoise(_seed ^ 0x7A3B9E1D4C5F6A8BL);
    }

    public bool Exists(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        if (chunkZ == Position.DefaultZ)
            return true;

        if (chunkZ == CaveZ)
            return TryGetCaveEntrance(chunkX, chunkY, out _, out _);

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetHeight(int wx, int wy)
    {
        // Main terrain: FBM for natural-looking caves
        double terrain = _terrainNoise.FBM(wx * TerrainScale, wy * TerrainScale, 4);
        // Detail layer adds small variation
        double detail = _detailNoise.FBM(wx * DetailScale, wy * DetailScale, 2);
        // Combine layers with weighting
        double height = terrain + detail * DetailWeight;
        return height;
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        if (chunkZ == CaveZ)
            return GenerateCave(chunkPos);

        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var rng = new SeededRandom(chunkSeed);

        // Three independent noise layers with different seeds

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));

        BiomeType GetBiomeAt(int wx, int wy)
        {
            double temp = _tempNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
            double moist = _moistNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
            return BiomeDefinitions.GetBiomeFromClimate(temp, moist);
        }

        // Pass 1: Carve terrain (floor vs wall vs liquid) using continuous noise
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                int wx = worldOffsetX + lx;
                int wy = worldOffsetY + ly;

                double height = GetHeight(wx, wy);

                var biome = GetBiomeAt(wx, wy);

                double resourceValue = _resourceNoise.FBM(wx * ResourceScale, wy * ResourceScale, 3);

                var canBeResourceNode = resourceValue > ResourceRockThreshold;
                var addedResourceNode = false;

                var liquidDef = BiomeDefinitions.GetLiquid(biome);

                ref var tile = ref chunk.Tiles[lx, ly];
                if (height < LiquidHeightThreshold && liquidDef != null)
                {
                    // Liquid
                    var l = liquidDef.Value;
                    tile.Type = l.Type;
                    tile.GlyphId = l.GlyphId;
                    tile.FgColor = l.FgColor;
                    tile.BgColor = l.BgColor;
                }
                else if (height > FloorThreshold)
                {
                    // Floor
                    tile.Type = TileType.Floor;
                    tile.GlyphId = TileDefinitions.GlyphFloor;
                    tile.FgColor = TileDefinitions.ColorFloorFg;
                    tile.BgColor = TileDefinitions.ColorBlack;
                }
                else
                {
                    // Walls

                    // Rocks spawn where resource noise overlaps with walls
                    if (canBeResourceNode)
                    {
                        // Resource node, put floor and a resource node on top
                        tile.Type = TileType.Floor;
                        tile.GlyphId = TileDefinitions.GlyphFloor;
                        tile.FgColor = TileDefinitions.ColorFloorFg;
                        tile.BgColor = TileDefinitions.ColorBlack;
                        result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), ResourceNodeDefinitions.PickRock(rng, biome)));
                        addedResourceNode = true;
                    }
                    else
                    {
                        tile.Type = TileType.Blocked;
                        tile.GlyphId = TileDefinitions.GlyphWall;
                        tile.FgColor = TileDefinitions.ColorWallFg;
                        tile.BgColor = TileDefinitions.ColorBlack;
                    }
                }

                if (tile.Type == TileType.Blocked || tile.Type == TileType.Floor)
                {
                    // Apply biome tint to walls and floors
                    tile.FgColor = tile.Type == TileType.Floor ? BiomeDefinitions.GetFloorColor(biome) : BiomeDefinitions.ApplyBiomeTint(tile.FgColor, biome);
                    tile.BgColor = BiomeDefinitions.ApplyBiomeTint(tile.BgColor, biome);
                }

                if (tile.Type == TileType.Floor && !addedResourceNode) // Only add features on floor tiles that don't have a resource node
                {
                    // Trees spawn where resource noise overlaps with floors
                    if (canBeResourceNode && rng.Next(100) < ResourceNodeDefinitions.BiomeTreeChance(biome))
                    {
                        result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ),
                            ResourceNodeDefinitions.All[ResourceNodeDefinitions.Tree]));
                    }
                    else
                    {
                        // Add decorative features
                        var decorations = BiomeDefinitions.GetDecorations(biome);
                        foreach (var deco in decorations)
                        {
                            if (rng.Next(1000) < deco.Chance1000)
                            {
                                tile.Type = TileType.Floor;
                                tile.GlyphId = deco.GlyphId;
                                tile.FgColor = BiomeDefinitions.ApplyBiomeTint(deco.FgColor, biome);
                                break;
                            }
                        }

                        // Add monster, items or torches
                        if (rng.Next(1000) < MonsterChance1000)
                        {
                            var def = BiomeDefinitions.PickEnemy(biome, rng, difficulty);
                            var monsterData = NpcDefinitions.GenerateMonsterData(def, difficulty);
                            result.Monsters.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), monsterData));
                        }
                        else if (rng.Next(1000) < ItemChance1000)
                        {
                            var loot = ItemDefinitions.GenerateLoot(rng, difficulty);
                            var itemData = ItemDefinitions.GenerateItemData(loot.Definition, loot.Rarity, rng);
                            result.Items.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), itemData));
                        }
                        else if (rng.Next(1000) < TorchChance1000)
                        {
                            result.Elements.Add(new DungeonElement(
                                Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ),
                                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                                new LightSource(6, TileDefinitions.ColorTorchFg)));
                        }

                    }
                }
            }
        }

        // Town generation: ~10% of non-origin chunks get a town
        if (TownGenerator.ShouldHaveTown(chunkX, chunkY, _seed))
        {
            // Determine biome at chunk center for construction material
            int centerWx = worldOffsetX + Chunk.Size / 2;
            int centerWy = worldOffsetY + Chunk.Size / 2;
            var townBiome = GetBiomeAt(centerWx, centerWy);
            TownGenerator.Generate(chunk, result, rng, townBiome, worldOffsetX, worldOffsetY, chunkZ);
        }

        if (chunkX == 0 && chunkY == 0)
        {
            // Find spawn point for starting chunk: look for a floor tile near the center, and clear nearby area for safety
            var spawnPoint = DungeonHelper.FindSpawnPoint(chunk);
            if (spawnPoint != null)
            {
                result.SpawnPosition = Position.FromCoords(spawnPoint.Value.X, spawnPoint.Value.Y, chunkZ);

                const int ClearRadius = 7;
                DungeonHelper.RemoveEnemiesInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius);
                DungeonHelper.MakeTilesFloorInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius);

                // Add torch
                result.Elements.Add(new DungeonElement(
                    Position.FromCoords(spawnPoint.Value.X, spawnPoint.Value.Y, chunkZ),
                    new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                    new LightSource(6, TileDefinitions.ColorTorchFg))
                );
            }
        }

        // Place cave entrance (StairsDown) on the surface if this chunk has a cave below
        if (TryGetCaveEntrance(chunkX, chunkY, out int entranceLx, out int entranceLy))
        {
            DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy);
            DungeonHelper.PlaceFeature(chunk, entranceLx, entranceLy, TileType.StairsDown,
                TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
        }

        return result;
    }

    /// <summary>
    /// Determines whether a chunk has a cave entrance and returns its local tile coordinates.
    /// Uses cave noise at the chunk center to decide presence, then picks a deterministic
    /// local position from the seed and validates it lands on a floor tile.
    /// </summary>
    private bool TryGetCaveEntrance(int chunkX, int chunkY, out int localX, out int localY)
    {
        localX = 0;
        localY = 0;

        int centerWx = chunkX * Chunk.Size + Chunk.Size / 2;
        int centerWy = chunkY * Chunk.Size + Chunk.Size / 2;
        double caveValue = _caveNoise.FBM(centerWx * CaveScale, centerWy * CaveScale, 2);

        if (caveValue <= CaveThreshold)
            return false;

        // Deterministic entrance position from seed + chunk coords
        long entranceSeed = _seed ^ ((long)chunkX * 0x6C078965 + (long)chunkY * 0x5D588B65 + 0x1234ABCD);
        var entranceRng = new SeededRandom(entranceSeed);
        // Keep away from chunk edges (margin of 3 tiles)
        const int margin = 3;
        localX = margin + entranceRng.Next(Chunk.Size - margin * 2);
        localY = margin + entranceRng.Next(Chunk.Size - margin * 2);

        // Validate the position is a floor tile using the same terrain logic as Generate()
        int wx = chunkX * Chunk.Size + localX;
        int wy = chunkY * Chunk.Size + localY;

        double height = GetHeight(wx, wy);

        // Must be on a floor tile (not wall, not liquid)
        bool entranceIsFloor = height > FloorThreshold;

        return entranceIsFloor;
    }

    /// <summary>
    /// Generates a BSP cave dungeon at DefaultZ - 1 with only an up-stair at the entrance position.
    /// </summary>
    private GenerationResult GenerateCave(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (!TryGetCaveEntrance(chunkX, chunkY, out int entranceLx, out int entranceLy))
            return result;

        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678) + chunkZ * 0x3C6EF35FL);
        var rng = new SeededRandom(chunkSeed);
        int size = Chunk.Size;
        var biome = BiomeDefinitions.GetBiomeForChunk(chunkPos, _seed);

        DungeonHelper.FillWalls(chunk);

        // Build BSP tree
        var root = new BspNode(BspPadding, BspPadding, size - BspPadding * 2, size - BspPadding * 2);
        SplitBspNode(root, rng);

        // Create rooms in leaf nodes
        var rooms = new List<Room>();
        CreateBspRooms(root, rng, rooms);

        // Carve rooms into chunk
        foreach (var room in rooms)
            DungeonHelper.CarveRoom(chunk, room);

        // Connect rooms via BSP siblings
        ConnectBspRooms(root, chunk, rng);

        // Place entrance stair (up only — can't go deeper)
        DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy);
        DungeonHelper.PlaceFeature(chunk, entranceLx, entranceLy, TileType.StairsUp,
            TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);

        // Connect the entrance stair to the nearest room
        if (rooms.Count > 0)
        {
            var nearest = rooms.OrderBy(r =>
                Math.Abs(r.CenterX - entranceLx) + Math.Abs(r.CenterY - entranceLy)).First();
            DungeonHelper.CarveCorridor(chunk, entranceLx, entranceLy,
                nearest.CenterX, nearest.CenterY, rng);
        }

        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);
        DungeonHelper.PlaceDecorations(chunk, biome, rng);
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY)) + 1; // +1 for being underground
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffsetX, worldOffsetY, chunkZ);
        DungeonHelper.ApplyBiomeTint(chunk, biome);

        return result;
    }

    #region BSP helpers (mirrored from BspDungeonGenerator)

    private static void SplitBspNode(BspNode node, SeededRandom rng)
    {
        if (node.Width < BspMinLeafSize * 2 && node.Height < BspMinLeafSize * 2)
            return;

        bool splitHorizontal;
        if (node.Width < BspMinLeafSize * 2) splitHorizontal = true;
        else if (node.Height < BspMinLeafSize * 2) splitHorizontal = false;
        else splitHorizontal = rng.Next(2) == 0;

        if (splitHorizontal)
        {
            if (node.Height < BspMinLeafSize * 2) return;
            int split = BspMinLeafSize + rng.Next(node.Height - BspMinLeafSize * 2 + 1);
            node.Left = new BspNode(node.X, node.Y, node.Width, split);
            node.Right = new BspNode(node.X, node.Y + split, node.Width, node.Height - split);
        }
        else
        {
            if (node.Width < BspMinLeafSize * 2) return;
            int split = BspMinLeafSize + rng.Next(node.Width - BspMinLeafSize * 2 + 1);
            node.Left = new BspNode(node.X, node.Y, split, node.Height);
            node.Right = new BspNode(node.X + split, node.Y, node.Width - split, node.Height);
        }

        SplitBspNode(node.Left, rng);
        SplitBspNode(node.Right, rng);
    }

    private static void CreateBspRooms(BspNode node, SeededRandom rng, List<Room> rooms)
    {
        if (node.Left != null && node.Right != null)
        {
            CreateBspRooms(node.Left, rng, rooms);
            CreateBspRooms(node.Right, rng, rooms);
            return;
        }

        int roomW = BspMinRoomSize + rng.Next(Math.Min(BspMaxRoomSize, node.Width - 2) - BspMinRoomSize + 1);
        int roomH = BspMinRoomSize + rng.Next(Math.Min(BspMaxRoomSize, node.Height - 2) - BspMinRoomSize + 1);
        int roomX = node.X + 1 + rng.Next(node.Width - roomW - 2 + 1);
        int roomY = node.Y + 1 + rng.Next(node.Height - roomH - 2 + 1);

        var room = new Room(roomX, roomY, roomW, roomH);
        node.Room = room;
        rooms.Add(room);
    }

    private static void ConnectBspRooms(BspNode node, Chunk chunk, SeededRandom rng)
    {
        if (node.Left == null || node.Right == null) return;

        ConnectBspRooms(node.Left, chunk, rng);
        ConnectBspRooms(node.Right, chunk, rng);

        var leftRoom = GetBspRoom(node.Left, rng);
        var rightRoom = GetBspRoom(node.Right, rng);
        if (leftRoom == null || rightRoom == null) return;

        DungeonHelper.CarveCorridor(chunk, leftRoom.CenterX, leftRoom.CenterY,
            rightRoom.CenterX, rightRoom.CenterY, rng);
    }

    private static Room? GetBspRoom(BspNode node, SeededRandom rng)
    {
        if (node.Room != null) return node.Room;
        if (node.Left == null && node.Right == null) return null;

        var leftRoom = node.Left != null ? GetBspRoom(node.Left, rng) : null;
        var rightRoom = node.Right != null ? GetBspRoom(node.Right, rng) : null;

        if (leftRoom == null) return rightRoom;
        if (rightRoom == null) return leftRoom;
        return rng.Next(2) == 0 ? leftRoom : rightRoom;
    }

    #endregion
}
