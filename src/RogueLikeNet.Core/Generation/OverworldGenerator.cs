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

    private readonly long _seed;

    public OverworldGenerator(long seed)
    {
        _seed = seed;
    }

    public GenerationResult Generate(int chunkX, int chunkY)
    {
        var chunk = new Chunk(chunkX, chunkY);
        long chunkSeed = _seed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678));
        var result = new GenerationResult(chunk);
        var rng = new SeededRandom(chunkSeed);

        // Three independent noise layers with different seeds
        var terrainNoise = new PerlinNoise(_seed);
        var detailNoise = new PerlinNoise(_seed ^ 0x5DEECE66DL);
        var tempNoise = new PerlinNoise(_seed ^ 0x27BB2EE687B0B0FDL);
        var moistNoise = new PerlinNoise(_seed ^ 0x12345678ABCDEF0L);
        var resourceNoise = new PerlinNoise(_seed ^ 0x3A4F5B6C7D8E9F0AL);

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));

        BiomeType GetBiomeAt(int wx, int wy)
        {
            double temp = tempNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
            double moist = moistNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
            return BiomeDefinitions.GetBiomeFromClimate(temp, moist);
        }

        // Pass 1: Carve terrain (floor vs wall vs liquid) using continuous noise
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                int wx = worldOffsetX + lx;
                int wy = worldOffsetY + ly;

                // Main terrain: FBM for natural-looking caves
                double terrain = terrainNoise.FBM(wx * TerrainScale, wy * TerrainScale, 4);
                // Detail layer adds small variation
                double detail = detailNoise.FBM(wx * DetailScale, wy * DetailScale, 2);
                // Combine layers with weighting
                double height = terrain + detail * DetailWeight;

                var biome = GetBiomeAt(wx, wy);

                double resourceValue = resourceNoise.FBM(wx * ResourceScale, wy * ResourceScale, 3);

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
                        result.ResourceNodes.Add((new Position(worldOffsetX + lx, worldOffsetY + ly), ResourceNodeDefinitions.PickRock(rng, biome)));
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
                        result.ResourceNodes.Add((new Position(worldOffsetX + lx, worldOffsetY + ly),
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
                            result.Monsters.Add((new Position(worldOffsetX + lx, worldOffsetY + ly), monsterData));
                        }
                        else if (rng.Next(1000) < ItemChance1000)
                        {
                            var loot = ItemDefinitions.GenerateLoot(rng, difficulty);
                            var itemData = ItemDefinitions.GenerateItemData(loot.Definition, loot.Rarity, rng);
                            result.Items.Add((new Position(worldOffsetX + lx, worldOffsetY + ly), itemData));
                        }
                        else if (rng.Next(1000) < TorchChance1000)
                        {
                            result.Elements.Add(new DungeonElement(
                                new Position(worldOffsetX + lx, worldOffsetY + ly),
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
            TownGenerator.Generate(chunk, result, rng, townBiome, worldOffsetX, worldOffsetY);
        }

        if (chunkX == 0 && chunkY == 0)
        {
            // Find spawn point for starting chunk: look for a floor tile near the center, and clear nearby area for safety
            var spawnPoint = DungeonHelper.FindSpawnPoint(chunk);
            if (spawnPoint != null)
            {
                result.SpawnPosition = spawnPoint.Value;

                const int ClearRadius = 7;
                DungeonHelper.RemoveEnemiesInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius);
                DungeonHelper.MakeTilesFloorInRadius(result, spawnPoint.Value.X, spawnPoint.Value.Y, ClearRadius);

                // Add torch
                result.Elements.Add(new DungeonElement(
                    new Position(spawnPoint.Value.X, spawnPoint.Value.Y),
                    new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                    new LightSource(6, TileDefinitions.ColorTorchFg))
                );
            }
        }

        return result;
    }

}
