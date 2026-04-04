using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a showcase room containing every item type at every rarity.
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

    public bool Exists(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        // Only the spawn chunk has content; all other chunks are empty floors.
        return chunkZ == Position.DefaultZ;
    }

    public GenerationResult Generate(Position chunkPos)
    {
        var (chunkX, chunkY, chunkZ) = chunkPos;
        var chunk = new Chunk(chunkPos);
        var result = new GenerationResult(chunk);

        if (chunkZ != Position.DefaultZ)
            return result;

        // Fill with floor
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }

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
        result.Elements.Add(new DungeonElement(
            Position.FromCoords(worldOffsetX + Chunk.Size / 2, worldOffsetY + 3, chunkZ),
            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
            new LightSource(20, TileDefinitions.ColorTorchFg)));

        // Place items in a grid: rows = item types, columns = rarities
        // Start at (4, 5) with 4-tile spacing
        int startX = 4;
        int startY = 6;
        int spacingX = 4;
        int spacingY = 3;

        string[] rarityNames = ["Common", "Uncommon", "Rare", "Epic", "Legendary"];

        // Place rarity labels as torches (visual markers) along the top
        for (int r = 0; r <= 4; r++)
        {
            int wx = worldOffsetX + startX + r * spacingX;
            int wy = worldOffsetY + startY - 2;
            // Use colored torches as column markers
            int color = r switch
            {
                0 => TileDefinitions.ColorWhite,
                1 => TileDefinitions.ColorGreen,
                2 => TileDefinitions.ColorBlue,
                3 => TileDefinitions.ColorMagenta,
                4 => TileDefinitions.ColorYellow,
                _ => TileDefinitions.ColorWhite,
            };
            result.Elements.Add(new DungeonElement(
                Position.FromCoords(wx, wy, chunkZ),
                new TileAppearance(TileDefinitions.GlyphTorch, color),
                new LightSource(4, color)));
        }

        var rng = new SeededRandom(_seed);

        for (int itemIdx = 0; itemIdx < ItemDefinitions.All.Length; itemIdx++)
        {
            var def = ItemDefinitions.All[itemIdx];
            for (int rarity = 0; rarity <= 4; rarity++)
            {
                int lx = startX + rarity * spacingX;
                int ly = startY + itemIdx * spacingY;

                if (lx >= Chunk.Size - 2 || ly >= Chunk.Size - 2)
                    continue;

                int rarityMult = 100 + rarity * 50;
                result.Items.Add((Position.FromCoords(worldOffsetX + lx, worldOffsetY + ly, chunkZ), new ItemData
                {
                    ItemTypeId = def.TypeId,
                    Rarity = ItemDefinitions.CapRarity(def.Category, rarity),
                    BonusAttack = def.BaseAttack * rarityMult / 100,
                    BonusDefense = def.BaseDefense * rarityMult / 100,
                    BonusHealth = def.BaseHealth * rarityMult / 100,
                    StackCount = def.Stackable
                        ? (def.Category == ItemDefinitions.CategoryGold ? 10 + rng.Next(50) : 1)
                        : 1,
                }));
            }
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
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Blocked;
        tile.GlyphId = TileDefinitions.GlyphWall;
        tile.FgColor = TileDefinitions.ColorWallFg;
        tile.BgColor = TileDefinitions.ColorBlack;
    }
}
