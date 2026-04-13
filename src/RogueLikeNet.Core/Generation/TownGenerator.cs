using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a town in the center of an overworld chunk using data-driven
/// structure templates and town type definitions loaded from JSON.
/// Biome determines construction materials. Spawns peaceful town NPCs with roles.
/// </summary>
internal static class TownGenerator
{
    private const int GapBetweenStructures = 3;

    public static SeededRandom GetSeededRandomForChunk(ChunkPosition chunkPos, long worldSeed)
    {
        var (chunkX, chunkY, _) = chunkPos;
        return new SeededRandom(worldSeed ^ (((long)chunkX * 0x45D9F3B) + ((long)chunkY * 0x12345678)));
    }

    /// <summary>
    /// Generates a town in the chunk. Call after terrain generation but before enemy spawning.
    /// Picks a town type based on biome, places structures from templates, and spawns NPCs.
    /// </summary>
    public static void Generate(Chunk chunk, GenerationResult result, SeededRandom rng, BiomeType biome,
        int worldOffsetX, int worldOffsetY, int worldZ)
    {
        var gameData = GameData.Instance;

        // Pick a town type eligible for this biome
        var townDef = gameData.Towns.PickRandom(biome, rng);
        if (townDef == null)
            return; // No town types available

        // Get biome material IDs
        int biomeWallId = gameData.Biomes.GetTownWallItemId(biome);
        int biomeDoorId = gameData.Biomes.GetTownDoorItemId(biome);
        int biomeWindowId = gameData.Biomes.GetTownWindowItemId(biome);
        int biomeFloorId = gameData.Biomes.GetTownFloorItemId(biome);
        int floorTileId = gameData.Biomes.GetFloorTileId(biome);

        int townSize = townDef.MinTownSize + rng.Next(Math.Max(1, townDef.MaxTownSize - townDef.MinTownSize + 1));
        int townStart = (Chunk.Size - townSize) / 2;
        int townEnd = townStart + townSize;
        int townCenterX = worldOffsetX + Chunk.Size / 2;
        int townCenterY = worldOffsetY + Chunk.Size / 2;

        // Flatten the town area to floor
        for (int x = townStart; x < townEnd; x++)
        {
            for (int y = townStart; y < townEnd; y++)
            {
                if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                ref var tile = ref chunk.Tiles[x, y];
                if (tile.Type != TileType.Floor)
                {
                    tile.TileId = floorTileId;
                }
            }
        }

        // Remove any monsters/items/resource nodes in the town area
        result.Monsters.RemoveAll(m =>
        {
            int lx = m.Position.X - worldOffsetX;
            int ly = m.Position.Y - worldOffsetY;
            return lx >= townStart && lx < townEnd && ly >= townStart && ly < townEnd;
        });
        result.Items.RemoveAll(i =>
        {
            int lx = i.Position.X - worldOffsetX;
            int ly = i.Position.Y - worldOffsetY;
            return lx >= townStart && lx < townEnd && ly >= townStart && ly < townEnd;
        });
        result.ResourceNodes.RemoveAll(r =>
        {
            int lx = r.Position.X - worldOffsetX;
            int ly = r.Position.Y - worldOffsetY;
            return lx >= townStart && lx < townEnd && ly >= townStart && ly < townEnd;
        });

        // Place structures according to town definition rules
        var placedRects = new List<(int X, int Y, int W, int H)>();
        int structureNpcCount = 0;

        foreach (var rule in townDef.Structures)
        {
            int count = rule.MinCount + rng.Next(Math.Max(1, rule.MaxCount - rule.MinCount + 1));
            var candidates = gameData.Structures.GetByCategoryOrIds(rule.Category, rule.StructureIds);
            if (candidates.Count == 0) continue;

            for (int i = 0; i < count; i++)
            {
                var structure = candidates[rng.Next(candidates.Count)];
                int rotation = structure.AllowRotation ? rng.Next(4) : 0;
                StructurePlacer.GetRotatedSize(structure.Width, structure.Height, rotation, out int placedW, out int placedH);

                // Try to find a non-overlapping position
                bool placed = false;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    int sx = townStart + 2 + rng.Next(Math.Max(1, townSize - placedW - 4));
                    int sy = townStart + 2 + rng.Next(Math.Max(1, townSize - placedH - 4));

                    if (sx + placedW >= townEnd - 1 || sy + placedH >= townEnd - 1)
                        continue;

                    if (Overlaps(placedRects, sx, sy, placedW, placedH))
                        continue;

                    // Place it
                    placedRects.Add((sx, sy, placedW, placedH));
                    int npcsBefore = result.TownNpcs.Count;

                    StructurePlacer.Place(
                        chunk, structure, sx, sy, rotation,
                        biomeWallId, biomeDoorId, biomeWindowId, biomeFloorId,
                        result, rng, worldOffsetX, worldOffsetY, worldZ,
                        townCenterX, townCenterY, townSize / 2);

                    structureNpcCount += result.TownNpcs.Count - npcsBefore;
                    placed = true;
                    break;
                }

                if (!placed) break; // Stop trying this structure rule if we can't place
            }
        }

        // Place a torch in the town center
        int townCenterLx = townCenterX - worldOffsetX;
        int townCenterLy = townCenterY - worldOffsetY;
        if (townCenterLx >= 0 && townCenterLx < Chunk.Size && townCenterLy >= 0 && townCenterLy < Chunk.Size)
        {
            ref var centerTile = ref chunk.Tiles[townCenterLx, townCenterLy];
            if (centerTile.PlaceableItemId == 0)
                centerTile.PlaceableItemId = gameData.Items.GetNumericId("torch_placeable");
        }

        // Spawn remaining NPCs as generic villagers in open spaces
        int targetNpcs = townDef.MinNpcs + rng.Next(Math.Max(1, townDef.MaxNpcs - townDef.MinNpcs + 1));
        int remainingNpcs = Math.Max(0, targetNpcs - structureNpcCount);

        for (int i = 0; i < remainingNpcs; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int nx = townStart + 2 + rng.Next(townSize - 4);
                int ny = townStart + 2 + rng.Next(townSize - 4);
                if (nx < 0 || nx >= Chunk.Size || ny < 0 || ny >= Chunk.Size) continue;
                ref var tile = ref chunk.Tiles[nx, ny];
                if (tile.Type != TileType.Floor) continue;
                if (tile.PlaceableItemId != 0 && !gameData.Items.IsPlaceableWalkable(tile.PlaceableItemId, tile.PlaceableItemExtra))
                    continue;

                result.TownNpcs.Add((
                    Position.FromCoords(worldOffsetX + nx, worldOffsetY + ny, worldZ),
                    TownNpcDefinitions.PickName(rng),
                    townCenterX, townCenterY, townSize / 2,
                    TownNpcRole.Villager
                ));
                break;
            }
        }
    }

    private static bool Overlaps(List<(int X, int Y, int W, int H)> rects, int x, int y, int w, int h)
    {
        foreach (var (rx, ry, rw, rh) in rects)
        {
            if (x - GapBetweenStructures < rx + rw && x + w + GapBetweenStructures > rx &&
                y - GapBetweenStructures < ry + rh && y + h + GapBetweenStructures > ry)
                return true;
        }
        return false;
    }
}
