using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a farming showcase map: open green fields with tilled soil plots,
/// crops at various growth stages, farm animals, and farming tools/seeds on the ground.
/// The spawn chunk (0,0) has the full farm layout; surrounding chunks are empty grass.
/// </summary>
public class FarmingShowcaseGenerator : IDungeonGenerator
{
    private readonly long _seed;

    public FarmingShowcaseGenerator(long seed)
    {
        _seed = seed;
    }

    public bool Exists(ChunkPosition chunkPos) => chunkPos.Z == Position.DefaultZ;

    public GenerationResult Generate(ChunkPosition chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // Fill entire chunk with grass floor
        FillGrass(chunk);

        if (chunkX != 0 || chunkY != 0)
            return result;

        var rng = new SeededRandom(_seed);

        // Spawn at center
        int centerX = Chunk.Size / 2;
        int centerY = Chunk.Size / 2;
        result.SpawnPosition = Position.FromCoords(worldOffsetX + centerX, worldOffsetY + centerY, chunkZ);

        // ── Layout: Four quadrants around center ──
        // NW: Crop field with tilled soil and crops at all stages
        // NE: Animal pasture with fenced pens
        // SW: Tool & seed supply area
        // SE: Mixed demo area with watered crops

        // Clear a central path (cross shape)
        // Horizontal and vertical paths already grass, just leave them

        // ── NW Quadrant: Crop Field (x=4..28, y=4..28) ──
        BuildCropField(chunk, result, rng, worldOffsetX, worldOffsetY, chunkZ,
            fieldX: 4, fieldY: 4, fieldW: 24, fieldH: 24);

        // ── NE Quadrant: Animal Pasture (x=36..60, y=4..28) ──
        BuildAnimalPasture(chunk, result, worldOffsetX, worldOffsetY, chunkZ,
            penX: 36, penY: 4, penW: 24, penH: 24);

        // ── SW Quadrant: Tool & Seed Supply (x=4..28, y=36..58) ──
        PlaceToolsAndSeeds(result, worldOffsetX, worldOffsetY, chunkZ,
            areaX: 4, areaY: 36);

        // ── SE Quadrant: Watered Crop Demo (x=36..60, y=36..58) ──
        BuildWateredCropDemo(chunk, result, rng, worldOffsetX, worldOffsetY, chunkZ,
            fieldX: 36, fieldY: 36, fieldW: 24, fieldH: 20);

        // Place torches for lighting
        PlaceTorches(result, worldOffsetX, worldOffsetY, chunkZ);

        return result;
    }

    private static void FillGrass(Chunk chunk)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.TileId = GameData.Instance.Tiles.GetNumericId("floor");
            }
    }

    /// <summary>
    /// Builds a crop field with rows of tilled soil. Some rows have crops at various growth stages.
    /// </summary>
    private static void BuildCropField(Chunk chunk, GenerationResult result, SeededRandom rng,
        int worldOffsetX, int worldOffsetY, int chunkZ,
        int fieldX, int fieldY, int fieldW, int fieldH)
    {
        var seedIds = new[] { "wheat_seeds", "corn_seeds", "potato_seeds", "carrot_seeds", "pumpkin_seeds", "berry_seeds" };

        // Create rows of tilled soil with 1-tile spacing
        int rowIndex = 0;
        for (int y = fieldY + 1; y < fieldY + fieldH - 1; y += 2)
        {
            string seedId = seedIds[rowIndex % seedIds.Length];
            var seedDef = GameData.Instance.Items.Get(seedId);

            for (int x = fieldX + 1; x < fieldX + fieldW - 1; x++)
            {
                // Till the soil
                ref var tile = ref chunk.Tiles[x, y];
                tile.TileId = GameData.Instance.Tiles.GetNumericId("tilled_soil");

                // Determine what to plant based on position along the row
                float progress = (float)(x - fieldX) / fieldW;

                if (seedDef?.Seed != null && progress > 0.1f)
                {
                    int growthTicks = seedDef.Seed.GrowthTicks;
                    int currentTicks;

                    if (progress < 0.3f)
                        currentTicks = 0; // Stage 0
                    else if (progress < 0.5f)
                        currentTicks = (int)(growthTicks * 0.4f); // Stage 1
                    else if (progress < 0.7f)
                        currentTicks = (int)(growthTicks * 0.7f); // Stage 2
                    else
                        currentTicks = growthTicks; // Stage 3 (mature)

                    var worldPos = Position.FromCoords(worldOffsetX + x, worldOffsetY + y, chunkZ);
                    result.Crops.Add((worldPos, seedDef, currentTicks, false));
                }
            }
            rowIndex++;
        }

        // Drop some seeds on the ground at the field entrance
        for (int i = 0; i < seedIds.Length; i++)
        {
            int seedNumId = GameData.Instance.Items.GetNumericId(seedIds[i]);
            if (seedNumId == 0) continue;
            var pos = Position.FromCoords(worldOffsetX + fieldX + 1 + i * 3, worldOffsetY + fieldY, chunkZ);
            result.Items.Add((pos, new ItemData { ItemTypeId = seedNumId, StackCount = 5 }));
        }
    }

    /// <summary>
    /// Builds fenced animal pens with animals inside.
    /// </summary>
    private static void BuildAnimalPasture(Chunk chunk, GenerationResult result,
        int worldOffsetX, int worldOffsetY, int chunkZ,
        int penX, int penY, int penW, int penH)
    {
        string[] animalTypes = ["chicken", "cow", "sheep"];
        int pensPerRow = animalTypes.Length;
        int penWidth = (penW - 2) / pensPerRow;
        int penHeight = penH - 4;

        for (int i = 0; i < animalTypes.Length; i++)
        {
            var animalDef = GameData.Instance.Animals.Get(animalTypes[i]);
            if (animalDef == null) continue;

            int px = penX + 1 + i * penWidth;
            int py = penY + 2;

            // Build fence
            for (int x = px; x < px + penWidth; x++)
            {
                SetFence(chunk, x, py);
                SetFence(chunk, x, py + penHeight - 1);
            }
            for (int y = py; y < py + penHeight; y++)
            {
                SetFence(chunk, px, y);
                SetFence(chunk, px + penWidth - 1, y);
            }

            // Leave a 1-tile gap as entrance
            ref var entrance = ref chunk.Tiles[px + penWidth / 2, py + penHeight - 1];
            entrance.PlaceableItemId = GameData.Instance.Items.GetNumericId("fence_gate");
            entrance.TileId = GameData.Instance.Tiles.GetNumericId("floor");

            // Place animals inside pen (3 of each type)
            for (int a = 0; a < 3; a++)
            {
                int ax = px + 2 + (a * 2) % (penWidth - 4);
                int ay = py + 2 + a;
                if (ax >= px + penWidth - 1 || ay >= py + penHeight - 1) continue;
                var pos = Position.FromCoords(worldOffsetX + ax, worldOffsetY + ay, chunkZ);
                result.Animals.Add((pos, animalDef));
            }

            // Place a feeding trough (decorative)
            ref var trough = ref chunk.Tiles[px + penWidth / 2, py + 1];
            trough.TileId = GameData.Instance.Tiles.GetNumericId("floor");
        }

        // Drop animal feed near the pens
        var feedId = GameData.Instance.Items.GetNumericId("animal_feed");
        if (feedId != 0)
        {
            var feedPos = Position.FromCoords(worldOffsetX + penX + 1, worldOffsetY + penY, chunkZ);
            result.Items.Add((feedPos, new ItemData { ItemTypeId = feedId, StackCount = 20 }));
        }
    }

    /// <summary>
    /// Places all farming tools and seed types on the ground in a neat grid.
    /// </summary>
    private static void PlaceToolsAndSeeds(GenerationResult result,
        int worldOffsetX, int worldOffsetY, int chunkZ,
        int areaX, int areaY)
    {
        // Tools row
        string[] toolIds = ["wooden_hoe", "iron_hoe", "watering_can"];
        for (int i = 0; i < toolIds.Length; i++)
        {
            int toolNumId = GameData.Instance.Items.GetNumericId(toolIds[i]);
            if (toolNumId == 0) continue;
            var pos = Position.FromCoords(worldOffsetX + areaX + i * 3, worldOffsetY + areaY, chunkZ);
            result.Items.Add((pos, new ItemData { ItemTypeId = toolNumId, StackCount = 1 }));
        }

        // Seeds row (below tools)
        string[] seedIds = ["wheat_seeds", "corn_seeds", "potato_seeds", "carrot_seeds", "pumpkin_seeds", "berry_seeds", "herb_seeds", "apple_sapling"];
        for (int i = 0; i < seedIds.Length; i++)
        {
            int seedNumId = GameData.Instance.Items.GetNumericId(seedIds[i]);
            if (seedNumId == 0) continue;
            var pos = Position.FromCoords(worldOffsetX + areaX + i * 3, worldOffsetY + areaY + 3, chunkZ);
            result.Items.Add((pos, new ItemData { ItemTypeId = seedNumId, StackCount = 10 }));
        }

        // Animal feed row
        int feedId = GameData.Instance.Items.GetNumericId("animal_feed");
        if (feedId != 0)
        {
            var pos = Position.FromCoords(worldOffsetX + areaX, worldOffsetY + areaY + 6, chunkZ);
            result.Items.Add((pos, new ItemData { ItemTypeId = feedId, StackCount = 50 }));
        }
    }

    /// <summary>
    /// Builds a demo area with pre-watered crops showing faster growth.
    /// </summary>
    private static void BuildWateredCropDemo(Chunk chunk, GenerationResult result, SeededRandom rng,
        int worldOffsetX, int worldOffsetY, int chunkZ,
        int fieldX, int fieldY, int fieldW, int fieldH)
    {
        var seedDef = GameData.Instance.Items.Get("wheat_seeds");
        if (seedDef?.Seed == null) return;

        for (int y = fieldY + 1; y < fieldY + fieldH - 1; y += 2)
        {
            for (int x = fieldX + 1; x < fieldX + fieldW - 1; x++)
            {
                // Till the soil
                ref var tile = ref chunk.Tiles[x, y];
                tile.TileId = GameData.Instance.Tiles.GetNumericId("tilled_soil");

                // Plant watered crops at varying stages
                float progress = (float)(x - fieldX) / fieldW;
                int growthTicks = seedDef.Seed.GrowthTicks;
                int currentTicks = (int)(growthTicks * progress);

                var worldPos = Position.FromCoords(worldOffsetX + x, worldOffsetY + y, chunkZ);
                result.Crops.Add((worldPos, seedDef, currentTicks, true));
            }
        }
    }

    private static void PlaceTorches(GenerationResult result, int worldOffsetX, int worldOffsetY, int chunkZ)
    {
        int torchId = GameData.Instance.Items.GetNumericId("torch_placeable");
        int[][] positions = [[16, 2], [48, 2], [16, 32], [48, 32], [32, 16], [32, 48],
                              [4, 4], [60, 4], [4, 60], [60, 60]];
        foreach (var pos in positions)
        {
            result.Chunk.Tiles[pos[0], pos[1]].PlaceableItemId = torchId;
        }
    }

    private static void SetFence(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.PlaceableItemId = GameData.Instance.Items.GetNumericId("fence");
    }
}
