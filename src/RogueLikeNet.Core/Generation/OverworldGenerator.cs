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
    private const double TerrainScale = 0.035;
    private const double BiomeScale = 0.012;

    // Terrain threshold: noise > this = floor (lower = more open)
    private const double FloorThreshold = -0.15;

    // Cave detail layer adds small pockets of wall/floor
    private const double DetailScale = 0.10;
    private const double DetailWeight = 0.15;

    // Liquid noise layer
    private const double LiquidScale = 0.08;
    private const double LiquidThreshold = 0.55;

    // Spawn density: chance per floor tile (out of 1000)
    private const int MonsterChance = 4;
    private const int ItemChance = 1;
    private const int TorchChance = 0; // Disabled for now, it doesn't make sense for overworld

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
        var liquidNoise = new PerlinNoise(_seed ^ 0x3141592653589793L);

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));

        // Pass 1: Carve terrain (floor vs wall) using continuous noise
        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                double wx = worldOffsetX + lx;
                double wy = worldOffsetY + ly;

                // Main terrain: FBM for natural-looking caves
                double terrain = terrainNoise.FBM(wx * TerrainScale, wy * TerrainScale, 4);
                // Detail layer adds small variation
                double detail = detailNoise.FBM(wx * DetailScale, wy * DetailScale, 2);
                double combined = terrain + detail * DetailWeight;

                ref var tile = ref chunk.Tiles[lx, ly];
                if (combined > FloorThreshold)
                {
                    tile.Type = TileType.Floor;
                    tile.GlyphId = TileDefinitions.GlyphFloor;
                    tile.FgColor = TileDefinitions.ColorFloorFg;
                    tile.BgColor = TileDefinitions.ColorBlack;
                }
                else
                {
                    tile.Type = TileType.Wall;
                    tile.GlyphId = TileDefinitions.GlyphWall;
                    tile.FgColor = TileDefinitions.ColorWallFg;
                    tile.BgColor = TileDefinitions.ColorBlack;
                }
            }

        // Pass 2: Determine per-tile biome and apply tint + decorations + liquids
        for (int lx = 0; lx < Chunk.Size; lx++)
            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                double wx = worldOffsetX + lx;
                double wy = worldOffsetY + ly;

                // Temperature/moisture for biome selection
                double temp = tempNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
                double moist = moistNoise.FBM(wx * BiomeScale, wy * BiomeScale, 3);
                var biome = BiomeDefinitions.GetBiomeFromClimate(temp, moist);

                ref var tile = ref chunk.Tiles[lx, ly];

                // Apply biome tint to all tiles (walls and floors)
                tile.FgColor = BiomeDefinitions.ApplyBiomeTint(tile.FgColor, biome);
                tile.BgColor = BiomeDefinitions.ApplyBiomeTint(tile.BgColor, biome);

                if (tile.Type != TileType.Floor) continue;

                // Liquid placement using noise for natural pools
                var liquidDef = BiomeDefinitions.GetLiquid(biome);
                if (liquidDef != null)
                {
                    double liq = liquidNoise.FBM(wx * LiquidScale, wy * LiquidScale, 2);
                    if (liq > LiquidThreshold)
                    {
                        var l = liquidDef.Value;
                        tile.Type = l.Type;
                        tile.GlyphId = l.GlyphId;
                        tile.FgColor = l.FgColor;
                        tile.BgColor = l.BgColor;
                        continue; // Don't place decorations or spawns on liquid
                    }
                }

                // Decorations — use RNG seeded per-tile for determinism
                var decorations = BiomeDefinitions.GetDecorations(biome);
                if (decorations.Length > 0)
                {
                    foreach (var deco in decorations)
                    {
                        if (rng.Next(100) < deco.Chance)
                        {
                            tile.Type = TileType.Decoration;
                            tile.GlyphId = deco.GlyphId;
                            tile.FgColor = BiomeDefinitions.ApplyBiomeTint(deco.FgColor, biome);
                            break;
                        }
                    }
                }

                // Spawn points on plain floor tiles (not liquid, not decoration)
                if (tile.Type == TileType.Floor)
                {
                    if (rng.Next(1000) < MonsterChance)
                    {
                        var def = NpcDefinitions.Pick(rng, difficulty);
                        int hpScale = 1 + difficulty / 2;
                        result.Monsters.Add((new Position(worldOffsetX + lx, worldOffsetY + ly), new MonsterData
                        {
                            MonsterTypeId = def.TypeId,
                            Health = def.Health * hpScale,
                            Attack = def.Attack + difficulty,
                            Defense = def.Defense + difficulty / 2,
                            Speed = def.Speed,
                        }));
                    }
                    else if (rng.Next(1000) < ItemChance)
                    {
                        var loot = ItemDefinitions.GenerateLoot(rng, difficulty);
                        int rarityMult = 100 + loot.Rarity * 50;
                        result.Items.Add((new Position(worldOffsetX + lx, worldOffsetY + ly), new ItemData
                        {
                            ItemTypeId = loot.Definition.TypeId,
                            Rarity = loot.Rarity,
                            BonusAttack = loot.Definition.BaseAttack * rarityMult / 100,
                            BonusDefense = loot.Definition.BaseDefense * rarityMult / 100,
                            BonusHealth = loot.Definition.BaseHealth * rarityMult / 100,
                            StackCount = loot.Definition.Stackable
                                ? (loot.Definition.Category == ItemDefinitions.CategoryGold ? 10 + rng.Next(50) : 1)
                                : 1,
                        }));
                    }
                    else if (rng.Next(1000) < TorchChance)
                    {
                        result.Elements.Add(new DungeonElement(
                            new Position(worldOffsetX + lx, worldOffsetY + ly),
                            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                            new LightSource(6, TileDefinitions.ColorTorchFg)));
                    }
                }
            }
        // Spawn point: find a floor tile near the center of the origin chunk
        if (chunkX == 0 && chunkY == 0)
        {
            int midX = Chunk.Size / 2;
            int midY = Chunk.Size / 2;
            // Spiral outward from center until we find an open floor tile
            bool spawnFound = false;
            for (int radius = 0; radius < Chunk.Size / 2 && !spawnFound; radius++)
            {
                for (int dx = -radius; dx <= radius && !spawnFound; dx++)
                {
                    for (int dy = -radius; dy <= radius && !spawnFound; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        int cx = midX + dx, cy = midY + dy;
                        if (cx < 1 || cy < 1 || cx >= Chunk.Size - 1 || cy >= Chunk.Size - 1) continue;
                        if (chunk.Tiles[cx, cy].Type == TileType.Floor)
                        {
                            result.SpawnPosition = (worldOffsetX + cx, worldOffsetY + cy);
                            spawnFound = true;
                        }
                    }
                }
            }
        }
        return result;
    }
}
