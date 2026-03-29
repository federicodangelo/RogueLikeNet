using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a town in the center of an overworld chunk.
/// Places houses with walls, doors, windows, furniture, and floor tiles.
/// Biome determines construction material. Spawns peaceful town NPCs.
/// </summary>
internal static class TownGenerator
{
    /// <summary>Town area size (centered in chunk).</summary>
    private const int TownSize = 30;
    private const int HouseMinSize = 5;
    private const int HouseMaxSize = 8;
    private const int MaxHouses = 6;
    private const int NpcCount = 4;

    /// <summary>
    /// Determines if a chunk should contain a town based on coordinates and seed.
    /// Returns true for ~10% of non-origin chunks.
    /// </summary>
    public static bool ShouldHaveTown(int chunkX, int chunkY, long seed)
    {
        // Never place a town at the origin chunk (player spawn)
        if (chunkX == 0 && chunkY == 0) return false;

        long hash = chunkX * 48611L ^ chunkY * 29423L ^ seed * 0x3C79AC492BA7B908L;
        int roll = (int)((hash & 0x7FFFFFFFL) % 100);
        return roll < 10;
    }

    /// <summary>
    /// Generates a town in the chunk. Call after terrain generation but before enemy spawning.
    /// Flattens the center area, builds houses, and adds NPC spawn data.
    /// </summary>
    public static void Generate(Chunk chunk, GenerationResult result, SeededRandom rng, BiomeType biome,
        int worldOffsetX, int worldOffsetY)
    {
        var mat = GetMaterial(biome);
        int townStart = (Chunk.Size - TownSize) / 2;
        int townEnd = townStart + TownSize;
        int townCenterX = worldOffsetX + Chunk.Size / 2;
        int townCenterY = worldOffsetY + Chunk.Size / 2;

        // Flatten the town area to floor
        for (int x = townStart; x < townEnd; x++)
        {
            for (int y = townStart; y < townEnd; y++)
            {
                if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
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

        // Generate house positions (non-overlapping)
        var houses = new List<Room>();
        for (int attempt = 0; attempt < MaxHouses * 10 && houses.Count < MaxHouses; attempt++)
        {
            int w = HouseMinSize + rng.Next(HouseMaxSize - HouseMinSize + 1);
            int h = HouseMinSize + rng.Next(HouseMaxSize - HouseMinSize + 1);
            int hx = townStart + 2 + rng.Next(Math.Max(1, TownSize - w - 4));
            int hy = townStart + 2 + rng.Next(Math.Max(1, TownSize - h - 4));

            // Ensure the house fits within the chunk and doesn't overlap others (with 1-tile gap)
            if (hx + w >= townEnd - 1 || hy + h >= townEnd - 1) continue;
            bool overlaps = false;
            foreach (var existing in houses)
            {
                if (hx - 1 < existing.X + existing.Width && hx + w + 1 > existing.X &&
                    hy - 1 < existing.Y + existing.Height && hy + h + 1 > existing.Y)
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) continue;
            houses.Add(new Room(hx, hy, w, h));
        }

        // Build each house
        foreach (var house in houses)
        {
            BuildHouse(chunk, house, rng, mat, worldOffsetX, worldOffsetY, result);
        }

        // Place a torch in the town center
        result.Elements.Add(new DungeonElement(
            new Position(townCenterX, townCenterY),
            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
            new LightSource(8, TileDefinitions.ColorTorchFg)));

        // Spawn town NPCs in the town area
        for (int i = 0; i < NpcCount; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int nx = townStart + 2 + rng.Next(TownSize - 4);
                int ny = townStart + 2 + rng.Next(TownSize - 4);
                if (nx < 0 || nx >= Chunk.Size || ny < 0 || ny >= Chunk.Size) continue;
                if (chunk.Tiles[nx, ny].Type != TileType.Floor) continue;

                result.TownNpcs.Add((
                    new Position(worldOffsetX + nx, worldOffsetY + ny),
                    TownNpcDefinitions.PickName(rng),
                    townCenterX, townCenterY, TownSize / 2
                ));
                break;
            }
        }
    }

    private static void BuildHouse(Chunk chunk, Room house, SeededRandom rng, TownMaterial mat,
        int worldOffsetX, int worldOffsetY, GenerationResult result)
    {
        // Floor inside (inset by 1 for walls)
        for (int x = house.X + 1; x < house.X + house.Width - 1; x++)
        {
            for (int y = house.Y + 1; y < house.Y + house.Height - 1; y++)
            {
                if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloorTile;
                tile.FgColor = mat.FloorColor;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
        }

        // Walls
        for (int x = house.X; x < house.X + house.Width; x++)
        {
            SetWall(chunk, x, house.Y, mat);
            SetWall(chunk, x, house.Y + house.Height - 1, mat);
        }
        for (int y = house.Y; y < house.Y + house.Height; y++)
        {
            SetWall(chunk, house.X, y, mat);
            SetWall(chunk, house.X + house.Width - 1, y, mat);
        }

        // Place a door on a wall
        int doorSide = rng.Next(4); // 0=N, 1=S, 2=W, 3=E
        int doorX, doorY;
        switch (doorSide)
        {
            case 0: // North wall
                doorX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                doorY = house.Y;
                break;
            case 1: // South wall
                doorX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                doorY = house.Y + house.Height - 1;
                break;
            case 2: // West wall
                doorX = house.X;
                doorY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
            default: // East wall
                doorX = house.X + house.Width - 1;
                doorY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
        }
        if (doorX >= 0 && doorX < Chunk.Size && doorY >= 0 && doorY < Chunk.Size)
        {
            ref var doorTile = ref chunk.Tiles[doorX, doorY];
            doorTile.Type = TileType.DoorClosed;
            doorTile.GlyphId = TileDefinitions.GlyphDoorClosed;
            doorTile.FgColor = mat.WallColor;
        }

        // Place a window on the opposite wall from the door
        int windowSide = (doorSide + 2) % 4;
        int windowX, windowY;
        switch (windowSide)
        {
            case 0:
                windowX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                windowY = house.Y;
                break;
            case 1:
                windowX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                windowY = house.Y + house.Height - 1;
                break;
            case 2:
                windowX = house.X;
                windowY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
            default:
                windowX = house.X + house.Width - 1;
                windowY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
        }
        if (windowX >= 0 && windowX < Chunk.Size && windowY >= 0 && windowY < Chunk.Size &&
            (windowX != doorX || windowY != doorY))
        {
            ref var windowTile = ref chunk.Tiles[windowX, windowY];
            windowTile.Type = TileType.Window;
            windowTile.GlyphId = TileDefinitions.GlyphWindow;
            windowTile.FgColor = TileDefinitions.ColorWindowFg;
        }

        // Place furniture inside the house
        PlaceFurniture(chunk, house, rng, result, worldOffsetX, worldOffsetY);
    }

    private static void PlaceFurniture(Chunk chunk, Room house, SeededRandom rng,
        GenerationResult result, int worldOffsetX, int worldOffsetY)
    {
        int interiorX = house.X + 1;
        int interiorY = house.Y + 1;
        int interiorW = house.Width - 2;
        int interiorH = house.Height - 2;

        // Try placing a table near center
        TryPlaceDecoration(chunk, interiorX + interiorW / 2, interiorY + interiorH / 2,
            TileDefinitions.GlyphTable, TileDefinitions.ColorTableFg);

        // Try placing chairs around the table
        if (interiorW >= 3 && interiorH >= 3)
        {
            TryPlaceDecoration(chunk, interiorX + interiorW / 2 - 1, interiorY + interiorH / 2,
                TileDefinitions.GlyphChair, TileDefinitions.ColorChairFg);
            TryPlaceDecoration(chunk, interiorX + interiorW / 2 + 1, interiorY + interiorH / 2,
                TileDefinitions.GlyphChair, TileDefinitions.ColorChairFg);
        }

        // Bed in a corner
        TryPlaceDecoration(chunk, interiorX, interiorY,
            TileDefinitions.GlyphBed, TileDefinitions.ColorBedFg);

        // Bookshelf along a wall
        if (interiorW >= 3)
        {
            TryPlaceDecoration(chunk, interiorX + interiorW - 1, interiorY,
                TileDefinitions.GlyphBookshelf, TileDefinitions.ColorBookshelfFg);
        }

        // Torch inside the house
        int torchX = interiorX + interiorW / 2;
        int torchY = interiorY;
        if (torchX >= 0 && torchX < Chunk.Size && torchY >= 0 && torchY < Chunk.Size &&
            chunk.Tiles[torchX, torchY].Type == TileType.Floor)
        {
            result.Elements.Add(new DungeonElement(
                new Position(worldOffsetX + torchX, worldOffsetY + torchY),
                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                new LightSource(5, TileDefinitions.ColorTorchFg)));
        }
    }

    private static void TryPlaceDecoration(Chunk chunk, int x, int y, int glyphId, int fgColor)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type != TileType.Floor) return;
        tile.Type = TileType.Decoration;
        tile.GlyphId = glyphId;
        tile.FgColor = fgColor;
    }

    private static void SetWall(Chunk chunk, int x, int y, TownMaterial mat)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Wall;
        tile.GlyphId = TileDefinitions.GlyphWall;
        tile.FgColor = mat.WallColor;
        tile.BgColor = TileDefinitions.ColorBlack;
    }

    /// <summary>
    /// Construction material palette per biome.
    /// </summary>
    private static TownMaterial GetMaterial(BiomeType biome) => biome switch
    {
        BiomeType.Forest => new(TileDefinitions.ColorWoodFg, TileDefinitions.ColorWoodFg),
        BiomeType.Ice => new(TileDefinitions.ColorIronFg, TileDefinitions.ColorIceFg),
        BiomeType.Lava or BiomeType.Infernal => new(TileDefinitions.ColorIronFg, TileDefinitions.ColorIronFg),
        BiomeType.Arcane => new(TileDefinitions.ColorCopperFg, TileDefinitions.ColorCopperFg),
        BiomeType.Crypt => new(TileDefinitions.ColorStoneTileFg, TileDefinitions.ColorStoneTileFg),
        BiomeType.Sewer => new(TileDefinitions.ColorCopperFg, TileDefinitions.ColorStoneTileFg),
        BiomeType.Fungal => new(TileDefinitions.ColorWoodFg, TileDefinitions.ColorMushroomFg),
        BiomeType.Ruined => new(TileDefinitions.ColorStoneTileFg, TileDefinitions.ColorRubbleFg),
        _ => new(TileDefinitions.ColorStoneTileFg, TileDefinitions.ColorStoneTileFg), // Stone, etc.
    };

    private readonly record struct TownMaterial(int WallColor, int FloorColor);
}
