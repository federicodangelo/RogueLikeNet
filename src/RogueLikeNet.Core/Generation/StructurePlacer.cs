using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Stamps a StructureDefinition onto a Chunk at a given position, resolving
/// legend keys to item IDs and applying optional rotation (0°/90°/180°/270°).
/// </summary>
internal static class StructurePlacer
{
    /// <summary>
    /// Places a structure onto the chunk.
    /// </summary>
    /// <param name="chunk">Target chunk.</param>
    /// <param name="structure">Structure template to stamp.</param>
    /// <param name="startX">Local X in chunk for top-left of the (rotated) structure.</param>
    /// <param name="startY">Local Y in chunk for top-left of the (rotated) structure.</param>
    /// <param name="rotation">0=none, 1=90°CW, 2=180°, 3=270°CW.</param>
    /// <param name="biomeWallId">Biome's town wall item numeric ID.</param>
    /// <param name="biomeDoorId">Biome's town door item numeric ID.</param>
    /// <param name="biomeWindowId">Biome's town window item numeric ID.</param>
    /// <param name="biomeFloorId">Biome's town floor item numeric ID.</param>
    /// <param name="result">GenerationResult to add NPCs/items to.</param>
    /// <param name="rng">Seeded random for NPC names.</param>
    /// <param name="worldOffsetX">World X offset of the chunk.</param>
    /// <param name="worldOffsetY">World Y offset of the chunk.</param>
    /// <param name="worldZ">World Z level.</param>
    /// <param name="townCenterX">Town center X in world coords (for NPC wander).</param>
    /// <param name="townCenterY">Town center Y in world coords (for NPC wander).</param>
    /// <param name="wanderRadius">NPC wander radius.</param>
    public static void Place(
        Chunk chunk, StructureDefinition structure,
        int startX, int startY, int rotation,
        int biomeWallId, int biomeDoorId, int biomeWindowId, int biomeFloorId,
        GenerationResult result, SeededRandom rng,
        int worldOffsetX, int worldOffsetY, int worldZ,
        int townCenterX, int townCenterY, int wanderRadius)
    {
        int srcW = structure.Width;
        int srcH = structure.Height;
        GetRotatedSize(srcW, srcH, rotation, out int placedW, out int placedH);

        int floorTileId = GameData.Instance.Tiles.GetNumericId("floor");

        // Stamp grid cells
        for (int row = 0; row < srcH; row++)
        {
            if (row >= structure.Grid.Length) break;
            var gridRow = structure.Grid[row];

            for (int col = 0; col < srcW; col++)
            {
                if (col >= gridRow.Length) break;
                char ch = gridRow[col];
                string key = ch.ToString();

                if (!structure.Legend.TryGetValue(key, out var legendValue))
                    continue;

                // Apply rotation to get placed coordinates
                RotatePoint(col, row, srcW, srcH, rotation, out int px, out int py);
                int tileX = startX + px;
                int tileY = startY + py;

                if (tileX < 0 || tileX >= Chunk.Size || tileY < 0 || tileY >= Chunk.Size)
                    continue;

                ref var tile = ref chunk.Tiles[tileX, tileY];

                int itemId = ResolveLegendItem(legendValue, biomeWallId, biomeDoorId, biomeWindowId, biomeFloorId);
                var itemDef = GameData.Instance.Items.Get(itemId);

                int tileId = ResolveLegendTile(legendValue);
                var tileDef = GameData.Instance.Tiles.Get(tileId);

                if (legendValue == StructureDefinition.EmptyKey)
                {
                    tile.TileId = floorTileId;
                    tile.PlaceableItemId = 0;
                    tile.PlaceableItemExtra = 0;
                }
                else if (itemDef != null && itemDef.IsPlaceable)
                {
                    tile.TileId = floorTileId;
                    tile.PlaceableItemId = itemId;
                    tile.PlaceableItemExtra = 0;
                }
                else if (tileDef != null)
                {
                    tile.TileId = tileId;
                    tile.PlaceableItemId = 0;
                    tile.PlaceableItemExtra = 0;
                }
                else
                {
                    tile.TileId = floorTileId;
                    tile.PlaceableItemId = 0;
                    tile.PlaceableItemExtra = 0;
                }
            }
        }

        // Stamp NPC spawn points
        foreach (var npcDef in structure.Npcs)
        {
            RotatePoint(npcDef.X, npcDef.Y, srcW, srcH, rotation, out int px, out int py);
            int npcTileX = startX + px;
            int npcTileY = startY + py;

            if (npcTileX < 0 || npcTileX >= Chunk.Size || npcTileY < 0 || npcTileY >= Chunk.Size)
                continue;

            // Only place on walkable floor
            if (chunk.Tiles[npcTileX, npcTileY].Type != TileType.Floor)
                continue;

            result.TownNpcs.Add((
                Position.FromCoords(worldOffsetX + npcTileX, worldOffsetY + npcTileY, worldZ),
                TownNpcDefinitions.PickName(rng),
                townCenterX, townCenterY, wanderRadius,
                npcDef.Role
            ));
        }

        // Stamp ground items
        foreach (var itemDef in structure.GroundItems)
        {
            RotatePoint(itemDef.X, itemDef.Y, srcW, srcH, rotation, out int px, out int py);
            int itemTileX = startX + px;
            int itemTileY = startY + py;

            if (itemTileX < 0 || itemTileX >= Chunk.Size || itemTileY < 0 || itemTileY >= Chunk.Size)
                continue;

            var def = GameData.Instance.Items.Get(itemDef.ItemId);
            if (def == null) continue;

            result.Items.Add((
                Position.FromCoords(worldOffsetX + itemTileX, worldOffsetY + itemTileY, worldZ),
                LootGenerator.GenerateItemData(def, rng)
            ));
        }

        // Stamp crops from cropGrid + cropLegend
        if (structure.CropGrid != null && structure.CropLegend != null)
        {
            int cropTilledId = GameData.Instance.Tiles.GetNumericId("tilled_soil");
            for (int row = 0; row < srcH; row++)
            {
                if (row >= structure.CropGrid.Length) break;
                var cropRow = structure.CropGrid[row];
                for (int col = 0; col < srcW; col++)
                {
                    if (col >= cropRow.Length) break;
                    char ch = cropRow[col];
                    if (ch == '.') continue;
                    string key = ch.ToString();
                    if (!structure.CropLegend.TryGetValue(key, out var seedId)) continue;

                    var seedDef = GameData.Instance.Items.Get(seedId);
                    if (seedDef?.Seed == null) continue;

                    RotatePoint(col, row, srcW, srcH, rotation, out int px, out int py);
                    int tileX = startX + px;
                    int tileY = startY + py;
                    if (tileX < 0 || tileX >= Chunk.Size || tileY < 0 || tileY >= Chunk.Size) continue;

                    ref var tile = ref chunk.Tiles[tileX, tileY];
                    tile.TileId = cropTilledId;
                    tile.PlaceableItemId = 0;

                    result.Crops.Add((
                        Position.FromCoords(worldOffsetX + tileX, worldOffsetY + tileY, worldZ),
                        seedDef, seedDef.Seed.GrowthTicks, false
                    ));
                }
            }
        }

        // Stamp animals from animalGrid + animalLegend
        if (structure.AnimalGrid != null && structure.AnimalLegend != null)
        {
            for (int row = 0; row < srcH; row++)
            {
                if (row >= structure.AnimalGrid.Length) break;
                var animalRow = structure.AnimalGrid[row];
                for (int col = 0; col < srcW; col++)
                {
                    if (col >= animalRow.Length) break;
                    char ch = animalRow[col];
                    if (ch == '.') continue;
                    string key = ch.ToString();
                    if (!structure.AnimalLegend.TryGetValue(key, out var animalId)) continue;

                    var def = GameData.Instance.Animals.Get(animalId);
                    if (def == null) continue;

                    RotatePoint(col, row, srcW, srcH, rotation, out int px, out int py);
                    int tileX = startX + px;
                    int tileY = startY + py;
                    if (tileX < 0 || tileX >= Chunk.Size || tileY < 0 || tileY >= Chunk.Size) continue;

                    result.Animals.Add((
                        Position.FromCoords(worldOffsetX + tileX, worldOffsetY + tileY, worldZ),
                        def
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Returns the bounding box size of a structure after rotation.
    /// </summary>
    public static void GetRotatedSize(int width, int height, int rotation, out int rotatedWidth, out int rotatedHeight)
    {
        if (rotation == 1 || rotation == 3)
        {
            rotatedWidth = height;
            rotatedHeight = width;
        }
        else
        {
            rotatedWidth = width;
            rotatedHeight = height;
        }
    }

    /// <summary>
    /// Transforms a point from source grid coordinates to rotated placement coordinates.
    /// </summary>
    private static void RotatePoint(int x, int y, int srcWidth, int srcHeight, int rotation, out int rx, out int ry)
    {
        switch (rotation)
        {
            case 1: // 90° CW
                rx = srcHeight - 1 - y;
                ry = x;
                break;
            case 2: // 180°
                rx = srcWidth - 1 - x;
                ry = srcHeight - 1 - y;
                break;
            case 3: // 270° CW
                rx = y;
                ry = srcWidth - 1 - x;
                break;
            default: // 0° — no rotation
                rx = x;
                ry = y;
                break;
        }
    }

    /// <summary>
    /// Resolves a legend value to a numeric item ID.
    /// Special biome_* keys use the provided biome material IDs.
    /// </summary>
    private static int ResolveLegendItem(string legendValue, int biomeWallId, int biomeDoorId, int biomeWindowId, int biomeFloorId)
    {
        return legendValue switch
        {
            StructureDefinition.BiomeWallKey => biomeWallId,
            StructureDefinition.BiomeDoorKey => biomeDoorId,
            StructureDefinition.BiomeWindowKey => biomeWindowId,
            StructureDefinition.BiomeFloorKey => biomeFloorId,
            StructureDefinition.EmptyKey => 0,
            _ => GameData.Instance.Items.GetNumericId(legendValue),
        };
    }

    /// <summary>
    /// Resolves a legend value to a numeric tile ID.
    /// Special biome_* keys use the provided biome material IDs.
    /// </summary>
    private static int ResolveLegendTile(string legendValue)
    {
        return GameData.Instance.Tiles.GetNumericId(legendValue);
    }
}
