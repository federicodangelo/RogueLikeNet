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
    private const int TorchChance = 3;

    // Guaranteed initial spawns near the first floor tile
    private const int InitialMonsters = 3;
    private const int InitialTorches = 2;
    private const int InitialSpawnRadius = 6;

    public GenerationResult Generate(Chunk chunk, long seed)
    {
        var result = new GenerationResult();
        var rng = new SeededRandom(seed);

        // Three independent noise layers with different seeds
        var terrainNoise = new PerlinNoise(seed);
        var detailNoise = new PerlinNoise(seed ^ 0x5DEECE66DL);
        var tempNoise = new PerlinNoise(seed ^ 0x27BB2EE687B0B0FDL);
        var moistNoise = new PerlinNoise(seed ^ 0x12345678ABCDEF0L);
        var liquidNoise = new PerlinNoise(seed ^ 0x3141592653589793L);

        int worldOffsetX = chunk.ChunkX * Chunk.Size;
        int worldOffsetY = chunk.ChunkY * Chunk.Size;

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
                    result.SpawnPoints.Add(new SpawnPoint(lx, ly, SpawnType.Monster));
                else if (rng.Next(1000) < ItemChance)
                    result.SpawnPoints.Add(new SpawnPoint(lx, ly, SpawnType.Item));
                else if (rng.Next(1000) < TorchChance)
                    result.SpawnPoints.Add(new SpawnPoint(lx, ly, SpawnType.Torch));
            }
        }

        // Pass 3: Guarantee a cluster of spawns near the first floor tile.
        // This ensures the player always finds initial encounters nearby.
        PlaceInitialSpawns(chunk, rng, result);

        return result;
    }

    private static void PlaceInitialSpawns(Chunk chunk, SeededRandom rng, GenerationResult result)
    {
        // Find the first walkable tile (same logic as FindSpawnPosition)
        int anchorX = -1, anchorY = -1;
        for (int x = 0; x < Chunk.Size && anchorX < 0; x++)
        for (int y = 0; y < Chunk.Size && anchorX < 0; y++)
        {
            if (chunk.Tiles[x, y].Type == TileType.Floor)
            {
                anchorX = x;
                anchorY = y;
            }
        }

        if (anchorX < 0) return; // No floor at all (shouldn't happen)

        // Collect nearby floor tiles
        var candidates = new List<(int X, int Y)>();
        for (int dx = -InitialSpawnRadius; dx <= InitialSpawnRadius; dx++)
        for (int dy = -InitialSpawnRadius; dy <= InitialSpawnRadius; dy++)
        {
            int nx = anchorX + dx;
            int ny = anchorY + dy;
            if (nx >= 0 && nx < Chunk.Size && ny >= 0 && ny < Chunk.Size
                && chunk.Tiles[nx, ny].Type == TileType.Floor
                && (dx != 0 || dy != 0)) // Don't spawn on the anchor
            {
                candidates.Add((nx, ny));
            }
        }

        // Place guaranteed monsters
        int monstersPlaced = 0;
        for (int i = 0; i < candidates.Count && monstersPlaced < InitialMonsters; i++)
        {
            int idx = rng.Next(candidates.Count);
            var (cx, cy) = candidates[idx];
            result.SpawnPoints.Add(new SpawnPoint(cx, cy, SpawnType.Monster));
            candidates.RemoveAt(idx);
            monstersPlaced++;
        }

        // Place guaranteed torches
        int torchesPlaced = 0;
        for (int i = 0; i < candidates.Count && torchesPlaced < InitialTorches; i++)
        {
            int idx = rng.Next(candidates.Count);
            var (cx, cy) = candidates[idx];
            result.SpawnPoints.Add(new SpawnPoint(cx, cy, SpawnType.Torch));
            candidates.RemoveAt(idx);
            torchesPlaced++;
        }
    }
}
