using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a showcase with one room per biome, showing each biome's decorations,
/// liquid pools, and tinting. Useful for testing biome visuals side by side.
/// Only chunk (0,0) has content.
/// </summary>
public class BiomeShowcaseGenerator : IDungeonGenerator
{
    private readonly long _seed;

    public BiomeShowcaseGenerator(long seed)
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

        // Fill with floor
        int defaultFloorTileId = GameData.Instance.Tiles.GetNumericId("floor");
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.TileId = defaultFloorTileId;
            }

        if (chunkX != 0 || chunkY != 0)
            return result;

        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // stride = roomSize + 1 (west wall) + 1 (east wall) + 2 (gap tiles) = roomSize + 4
        // 2 cols at stride 16: east walls at 14, 30 — all < 64 ✓
        // 5 rows at stride 12, startY=1: south walls at 9,21,33,45,57 — all < 64 ✓
        // 2-tile gaps: col gap x=15,16; row gaps e.g. y=10,11 between rows 0 and 1 ✓
        const int roomW = 12;
        const int roomH = 8;
        const int strideX = 16; // roomW + 4
        const int strideY = 12; // roomH + 4
        const int startX = 2;
        const int startY = 1;   // north wall of first row at y = startY - 1 = 0
        const int cols = 2;

        // Spawn in the 2-tile gap between the two columns, at the top edge
        result.SpawnPosition = Position.FromCoords(worldOffsetX + startX + roomW + 1, worldOffsetY + 0, chunkZ);
        // x = 2 + 12 + 1 = 15 (gap tile between east wall x=14 and west wall x=17), y = 0

        // Broad torch at spawn for initial visibility
        result.Elements.Add(new DungeonElement(
            Position.FromCoords(worldOffsetX + startX + roomW + 1, worldOffsetY + 0, chunkZ),
            new TileAppearance(RenderConstants.GlyphTorch, RenderConstants.ColorTorchFg),
            new LightSource(30, RenderConstants.ColorTorchFg)));

        var biomes = (BiomeType[])Enum.GetValues(typeof(BiomeType));

        for (int i = 0; i < biomes.Length; i++)
        {
            var biome = biomes[i];
            int col = i % cols;
            int row = i / cols;
            int rx = startX + col * strideX;
            int ry = startY + row * strideY;

            if (rx + roomW > Chunk.Size - 1 || ry + roomH > Chunk.Size - 1)
                continue;

            // Walled room with 3-tile centered north opening
            BuildRoom(chunk, rx, ry, roomW, roomH, biome);

            // Apply biome floor tiles to interior
            int biomeFloorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);
            for (int x = rx; x < rx + roomW; x++)
                for (int y = ry; y < ry + roomH; y++)
                {
                    ref var tile = ref chunk.Tiles[x, y];
                    tile.TileId = biomeFloorTileId;
                }

            // Tint wall tiles on all four sides
            int biomeWallTileId = GameData.Instance.Biomes.GetWallTileId(biome);
            for (int x = rx - 1; x <= rx + roomW; x++)
            {
                SetBiomeWall(chunk, x, ry - 1, biomeWallTileId);
                SetBiomeWall(chunk, x, ry + roomH, biomeWallTileId);
            }
            for (int y = ry; y <= ry + roomH; y++)
            {
                SetBiomeWall(chunk, rx - 1, y, biomeWallTileId);
                SetBiomeWall(chunk, rx + roomW, y, biomeWallTileId);
            }

            // Decorations row near the south wall
            var decorations = GameData.Instance.Biomes.GetDecorations(biome);
            if (decorations.Length > 0)
            {
                int decoY = ry + roomH - 2;
                for (int d = 0; d < decorations.Length; d++)
                {
                    int dx = rx + 1 + d * 3;
                    if (dx >= rx + roomW - 1) break;
                    ref var tile = ref chunk.Tiles[dx, decoY];
                    tile.TileId = decorations[d].TileNumericId;
                }
            }

            // Liquid pool in the upper portion of the room
            var liquidDef = GameData.Instance.Biomes.GetLiquid(biome);
            if (liquidDef != null)
            {
                int poolX = rx + 3;
                int poolY = ry + 2;
                int poolW = Math.Min(4, roomW - 6);
                int poolH = Math.Min(2, roomH - 4);
                for (int x = poolX; x < poolX + poolW; x++)
                    for (int y = poolY; y < poolY + poolH; y++)
                    {
                        if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
                        {
                            ref var tile = ref chunk.Tiles[x, y];
                            tile.TileId = liquidDef.TileNumericId;
                        }
                    }
            }

            // Torch inside the room for FOV
            result.Elements.Add(new DungeonElement(
                Position.FromCoords(worldOffsetX + rx + 1, worldOffsetY + ry + 1, chunkZ),
                new TileAppearance(RenderConstants.GlyphTorch, RenderConstants.ColorTorchFg),
                new LightSource(8, RenderConstants.ColorTorchFg)));
        }

        return result;
    }

    /// <summary>
    /// Places walls around a room interior at (rx, ry) of size (rw x rh).
    /// North wall has a 3-tile centered opening; south, west, east walls are solid.
    /// Interior tiles remain as pre-filled floor.
    /// </summary>
    private static void BuildRoom(Chunk chunk, int rx, int ry, int rw, int rh, BiomeType biome)
    {
        int wallTileId = GameData.Instance.Biomes.GetWallTileId(biome);
        int openStart = (rw - 3) / 2;

        // North wall with centered 3-tile opening (lx = x - rx, range -1..rw)
        for (int x = rx - 1; x <= rx + rw; x++)
        {
            int lx = x - rx;
            if (lx < openStart || lx > openStart + 2)
                SetWall(chunk, x, ry - 1, wallTileId);
        }
        // South wall (full)
        for (int x = rx - 1; x <= rx + rw; x++)
            SetWall(chunk, x, ry + rh, wallTileId);
        // West and east walls
        for (int y = ry; y <= ry + rh; y++)
        {
            SetWall(chunk, rx - 1, y, wallTileId);
            SetWall(chunk, rx + rw, y, wallTileId);
        }
    }

    private static void SetWall(Chunk chunk, int x, int y, int wallTileId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.TileId = wallTileId;
    }

    private static void SetBiomeWall(Chunk chunk, int x, int y, int wallTileId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type == TileType.Blocked)
            tile.TileId = wallTileId;
    }
}
