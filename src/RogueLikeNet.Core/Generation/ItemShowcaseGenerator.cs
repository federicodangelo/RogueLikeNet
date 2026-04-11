using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a showcase room containing every item type
/// Only chunk (0,0) has content; all other chunks are empty floors.
/// Layout: a large room with items arranged in a grid — rows = item types, columns = rarities.
/// </summary>
public class ItemShowcaseGenerator : IDungeonGenerator
{
    private readonly long _seed;

    public ItemShowcaseGenerator(long seed)
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

        int floorTileId = GameData.Instance.Tiles.GetNumericId("floor");
        int wallTileId = GameData.Instance.Tiles.GetNumericId("wall");
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                chunk.Tiles[x, y].TileId = floorTileId;

        if (chunkX != 0 || chunkY != 0)
            return result;

        // Build walls around a large room
        int roomX = 1, roomY = 1;
        int roomW = Chunk.Size - 2, roomH = Chunk.Size - 2;
        BuildWallBorder(chunk, roomX, roomY, roomW, roomH);

        // Place a torch for lighting
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // Spawn point: just inside the room entrance at the top
        result.SpawnPosition = Position.FromCoords(worldOffsetX + Chunk.Size / 2, worldOffsetY + 3, chunkZ);
        chunk.Tiles[Chunk.Size / 2, 3].PlaceableItemId = GameData.Instance.Items.GetNumericId("torch_placeable");

        // Place items in a grid: rows = item types, columns = rarities
        // Start at (4, 5) with 4-tile spacing
        int startX = 4;
        int startY = 6;
        int spacingX = 2;
        int spacingY = 2;

        var rng = new SeededRandom(_seed);

        // Items

        var simpleItems = GameData.Instance.Items.All
            .Where(d => !d.IsPlaceable && d.Category != ItemCategory.Material)
            .ToArray();

        for (int itemIdx = 0; itemIdx < simpleItems.Length; itemIdx++)
        {
            var def = simpleItems[itemIdx];

            int lx = startX + itemIdx % 5 * spacingX;
            int ly = startY + itemIdx / 5 * spacingY;

            if (lx >= Chunk.Size - 2 || ly >= Chunk.Size - 2)
                continue;

            result.Items.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), new ItemData
            {
                ItemTypeId = def.NumericId,
                StackCount = def.Stackable
                    ? (def.Category == ItemCategory.Misc ? 10 + rng.Next(50) : 1)
                    : 1,
            }));
        }

        startX += 6 * spacingX; // Shift right for placeables and resource nodes

        // Placeables

        var allPlaceables = GameData.Instance.Items.GetAllPlaceables();
        for (int placeableIdx = 0; placeableIdx < allPlaceables.Length; placeableIdx++)
        {
            var def = allPlaceables[placeableIdx];
            if (def.NumericId == 0)
                continue;

            int lx = startX + placeableIdx % 5 * spacingX;
            int ly = startY + placeableIdx / 5 * spacingY;

            if (lx >= Chunk.Size - 2 || ly >= Chunk.Size - 2)
                continue;

            chunk.Tiles[lx, ly].PlaceableItemId = def.NumericId;
        }

        startX += 6 * spacingX; // Shift right for resource nodes

        // Resource nodes
        var allNodes = GameData.Instance.ResourceNodes.All.ToArray();
        for (int nodeIdx = 0; nodeIdx < allNodes.Length; nodeIdx++)
        {
            var def = allNodes[nodeIdx];
            if (def.NumericId == 0)
                continue;

            int lx = startX + nodeIdx % 5 * spacingX;
            int ly = startY + nodeIdx / 5 * spacingY;

            if (lx >= Chunk.Size - 2 || ly >= Chunk.Size - 2)
                continue;

            result.ResourceNodes.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), def));
        }

        // Resources
        startY += spacingY * ((allNodes.Length / 5) + 1);
        var resouceItems = GameData.Instance.Items.All.Where(d => d.Category == ItemCategory.Material).ToArray();
        for (int resourceIdx = 0; resourceIdx < resouceItems.Length; resourceIdx++)
        {
            var def = resouceItems[resourceIdx];

            int lx = startX + resourceIdx % 5 * spacingX;
            int ly = startY + resourceIdx / 5 * spacingY;

            if (lx >= Chunk.Size - 2 || ly >= Chunk.Size - 2)
                continue;

            int stackCount = 10 + rng.Next(50);
            result.Items.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), new ItemData
            {
                ItemTypeId = def.NumericId,
                StackCount = stackCount,
            }));
        }


        return result;
    }

    private static void BuildWallBorder(Chunk chunk, int rx, int ry, int rw, int rh)
    {
        for (int x = rx; x < rx + rw; x++)
        {
            SetWall(chunk, x, ry);
            SetWall(chunk, x, ry + rh - 1);
        }
        for (int y = ry; y < ry + rh; y++)
        {
            SetWall(chunk, rx, y);
            SetWall(chunk, rx + rw - 1, y);
        }
    }

    private static void SetWall(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        chunk.Tiles[x, y].TileId = GameData.Instance.Tiles.GetNumericId("wall");
    }
}
