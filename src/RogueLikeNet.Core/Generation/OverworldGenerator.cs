using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
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
            return TryGetCaveEntrance(chunkPos, out _, out _);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BiomeType GetBiomeAt(int wx, int wy)
    {
        double temp = _tempNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
        double moist = _moistNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
        return BiomeRegistry.GetBiomeFromClimate(temp, moist);
    }

    static private bool IsSpawnChunk(ChunkPosition chunkPos) =>
        chunkPos.X == 0 && chunkPos.Y == 0 && chunkPos.Z == Position.DefaultZ;

    private bool ShouldHaveTown(ChunkPosition chunkPos)
    {
        // Never place a town at the origin chunk (player spawn)
        if (IsSpawnChunk(chunkPos)) return false;

        var (chunkX, chunkY, _) = chunkPos;

        long hash = chunkX * 48611L ^ chunkY * 29423L ^ _seed * 0x3C79AC492BA7B908L;
        int roll = (int)((hash & 0x7FFFFFFFL) % 100);
        return roll < 10; // 10% chance
    }

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        if (chunkPos.Z == CaveZ)
            return GenerateCave(chunkPos);


        if (chunkPos.Z != Position.DefaultZ)
        {
            var chunk = new Chunk(chunkPos);
            var result = new GenerationResult(chunk);
            return result;
        }

        return GenerateOverworld(chunkPos);
    }

    private GenerationResult GenerateOverworld(ChunkPosition chunkPos)
    {
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        var (chunkX, chunkY, chunkZ) = chunkPos;

        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var rng = new SeededRandom(chunkSeed);

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));

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

                var liquidDef = GameData.Instance.Biomes.GetLiquid(biome);

                ref var tile = ref chunk.Tiles[lx, ly];
                if (height < LiquidHeightThreshold && liquidDef != null)
                {
                    tile.TileId = liquidDef.TileNumericId;
                }
                else if (height > FloorThreshold)
                {
                    tile.TileId = GameData.Instance.Biomes.GetFloorTileId(biome);
                }
                else
                {
                    if (canBeResourceNode)
                    {
                        tile.TileId = GameData.Instance.Biomes.GetFloorTileId(biome);
                        result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), GameData.Instance.ResourceNodes.PickRock(rng, biome)));
                        addedResourceNode = true;
                    }
                    else
                    {
                        tile.TileId = GameData.Instance.Biomes.GetWallTileId(biome);
                    }
                }

                if (tile.Type == TileType.Floor && !addedResourceNode) // Only add features on floor tiles that don't have a resource node
                {
                    // Trees spawn where resource noise overlaps with floors
                    if (canBeResourceNode && rng.Next(100) < GameData.Instance.ResourceNodes.BiomeTreeChance(biome))
                    {
                        result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ),
                            GameData.Instance.ResourceNodes.Get("tree")!));
                    }
                    else
                    {
                        // Add decorative features
                        var decorations = GameData.Instance.Biomes.GetDecorations(biome);
                        foreach (var deco in decorations)
                        {
                            if (rng.Next(1000) < deco.Chance1000)
                            {
                                tile.TileId = deco.TileNumericId;
                                break;
                            }
                        }

                        // Add monster, items or torches
                        if (rng.Next(1000) < MonsterChance1000)
                        {
                            var def = GameData.Instance.Biomes.PickEnemy(biome, rng, difficulty);
                            if (def == null) continue; // No valid monsters for this biome/difficulty, skip
                            var monsterData = NpcRegistry.GenerateMonsterData(def, difficulty);
                            result.Monsters.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), monsterData));
                        }
                        else if (rng.Next(1000) < ItemChance1000)
                        {
                            var loot = LootGenerator.GenerateLoot(rng, difficulty);
                            var itemData = LootGenerator.GenerateItemData(loot.Definition, rng);
                            result.Items.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), itemData));
                        }
                        else if (rng.Next(1000) < TorchChance1000)
                        {
                            chunk.Tiles[lx, ly].PlaceableItemId =
                                GameData.Instance.Items.GetNumericId("torch_placeable");
                        }
                    }
                }
            }
        }

        // Town generation: ~10% of non-origin chunks get a town
        if (ShouldHaveTown(chunkPos))
        {
            // Determine biome at chunk center for construction material
            int centerWx = worldOffsetX + Chunk.Size / 2;
            int centerWy = worldOffsetY + Chunk.Size / 2;
            var townBiome = GetBiomeAt(centerWx, centerWy);
            var townRng = TownGenerator.GetSeededRandomForChunk(chunkPos, _seed);
            TownGenerator.Generate(chunk, result, townRng, townBiome, worldOffsetX, worldOffsetY, chunkZ);
        }

        // Place cave entrance (StairsDown) on the surface if this chunk has a cave below
        if (TryGetCaveEntrance(chunkPos, out var entranceLx, out var entranceLy))
        {
            int entranceFloorTileId = GameData.Instance.Tiles.GetNumericId("floor");
            DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy, entranceFloorTileId);
            DungeonHelper.PlaceTile(chunk, entranceLx, entranceLy, GameData.Instance.Tiles.GetNumericId("stairs_down"));
        }

        if (IsSpawnChunk(chunkPos))
        {
            // Find spawn point for starting chunk: look for a floor tile near the center, and clear nearby area for safety
            var spawnPoint = DungeonHelper.FindSpawnPoint(chunk);
            if (spawnPoint != null)
            {
                result.SpawnPosition = Position.FromCoords(spawnPoint.Value.X, spawnPoint.Value.Y, chunkZ);

                const int ClearRadius = 7;
                DungeonHelper.RemoveEnemiesInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius);
                DungeonHelper.MakeTilesFloorInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius, GameData.Instance.Tiles.GetNumericId("floor"));

                // Add torch
                Chunk.WorldToLocal(spawnPoint.Value.X, spawnPoint.Value.Y, chunkX, chunkY, out var spLx, out var spLy);
                chunk.Tiles[spLx, spLy].PlaceableItemId =
                    GameData.Instance.Items.GetNumericId("torch_placeable");
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether a chunk has a cave entrance and returns its local tile coordinates.
    /// Uses cave noise at the chunk center to decide presence, then picks a deterministic
    /// local position from the seed and validates it lands on a floor tile.
    /// </summary>
    private bool TryGetCaveEntrance(ChunkPosition chunkPos, out int localX, out int localY)
    {
        var (chunkX, chunkY, _) = chunkPos;

        localX = 0;
        localY = 0;

        if (IsSpawnChunk(chunkPos))
        {
            localX = localY = 0;
            return false; // Don't place a cave entrance at the spawn chunk
        }

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

    private SeededRandom GetSeededRandomForChunk(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678) + chunkZ * 0x3C6EF35FL);
        return new SeededRandom(chunkSeed);
    }

    /// <summary>
    /// Generates a cave dungeon at DefaultZ - 1 with only an up-stair at the entrance position.
    /// Picks the layout generator based on the surface biome for variety.
    /// </summary>
    private GenerationResult GenerateCave(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (!TryGetCaveEntrance(chunkPos, out var entranceLx, out var entranceLy))
            return result;

        var entranceWorldPosition = chunk.LocalToWorld(entranceLx, entranceLy);

        var biome = GetBiomeAt(entranceWorldPosition.X, entranceWorldPosition.Y);
        int wallTileId = GameData.Instance.Biomes.GetWallTileId(biome);
        int floorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);

        // Pick layout generator based on biome (same dispatch as BiomeDungeonGenerator)
        var rng = GetSeededRandomForChunk(chunkPos);
        var layoutType = PickCaveLayout(rng);

        List<Room> rooms = [];
        var populateRoomParams = DungeonHelper.PopulateRoomsParams.Default;
        var placeResourceNodesParams = DungeonHelper.PlaceResourceNodesParams.Default;

        switch (layoutType)
        {
            case CaveLayoutType.CellularAutomata:
                rooms = CellularAutomataCaveGenerator.GenerateLayout(chunk, rng, wallTileId, floorTileId, entranceLx, entranceLy);
                populateRoomParams = new DungeonHelper.PopulateRoomsParams
                {
                    RoomMonsterChanceBase100 = 50,
                    MinMonsters = 1,
                    MaxMonsters = 2,
                    RoomLootChanceBase100 = 25,
                    RoomTorchChanceBase100 = 25
                };
                placeResourceNodesParams = new DungeonHelper.PlaceResourceNodesParams
                {
                    RoomNodesChanceBase100 = 25,
                    MinNodes = 1,
                    MaxNodes = 2,
                };
                break;

            case CaveLayoutType.DirectionalTunnel:
                rooms = DirectionalTunnelGenerator.GenerateLayout(chunk, rng, wallTileId, floorTileId, entranceLx, entranceLy);
                populateRoomParams = new DungeonHelper.PopulateRoomsParams
                {
                    RoomMonsterChanceBase100 = 50,
                    MinMonsters = 1,
                    MaxMonsters = 2,
                    RoomLootChanceBase100 = 25,
                    RoomTorchChanceBase100 = 25
                };
                placeResourceNodesParams = new DungeonHelper.PlaceResourceNodesParams
                {
                    RoomNodesChanceBase100 = 25,
                    MinNodes = 1,
                    MaxNodes = 2,
                };
                break;

            case CaveLayoutType.BspDungeon:
                rooms = BspDungeonGenerator.GenerateLayout(chunk, rng, wallTileId, floorTileId, entranceLx, entranceLy);
                populateRoomParams = new DungeonHelper.PopulateRoomsParams
                {
                    RoomMonsterChanceBase100 = 50,
                    MinMonsters = 1,
                    MaxMonsters = 2,
                    RoomLootChanceBase100 = 25,
                    RoomTorchChanceBase100 = 25
                };
                placeResourceNodesParams = new DungeonHelper.PlaceResourceNodesParams
                {
                    RoomNodesChanceBase100 = 25,
                    MinNodes = 1,
                    MaxNodes = 2,
                };
                break;
        }

        // Place entrance stair (up only — can't go deeper)
        DungeonHelper.CarveFloor(chunk, entranceLx, entranceLy, floorTileId);
        DungeonHelper.PlaceTile(chunk, entranceLx, entranceLy, GameData.Instance.Tiles.GetNumericId("stairs_up"));

        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY)) + 1; // +1 for being underground
        var worldOffset = chunk.LocalToWorld(0, 0);

        // Post-process the cave layout with liquids, decorations, monsters, and resources just like a normal dungeon
        DungeonHelper.PlaceLiquidPools(chunk, rooms, biome, rng);
        DungeonHelper.PlaceDecorations(chunk, biome, rng, result);
        DungeonHelper.PopulateRooms(rooms, rng, result, difficulty, worldOffset, populateRoomParams);
        DungeonHelper.PlaceResourceNodes(rooms, rng, result, biome, worldOffset, placeResourceNodesParams);

        return result;
    }

    private enum CaveLayoutType
    {
        CellularAutomata,
        DirectionalTunnel,
        BspDungeon
    }

    private static CaveLayoutType PickCaveLayout(SeededRandom rng)
    {
        return rng.Next(3) switch
        {
            0 => CaveLayoutType.CellularAutomata,
            1 => CaveLayoutType.DirectionalTunnel,
            _ => CaveLayoutType.BspDungeon
        };
    }
}
