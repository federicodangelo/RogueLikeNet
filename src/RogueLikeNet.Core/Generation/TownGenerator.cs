using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Generates a town in the center of an overworld chunk.
/// Places houses using actual buildable items so everything can be picked up.
/// Biome determines construction material. Spawns peaceful town NPCs.
/// </summary>
internal static class TownGenerator
{
    /// <summary>Town area size (centered in chunk).</summary>
    private const int MinTownSize = 20;
    private const int MaxTownSize = 50;

    private const int HouseMinSize = 5;
    private const int HouseMaxSize = 8;
    private const int MaxHouses = 12;

    private const int GapBetweenHouses = 3; // Minimum gap between houses to prevent overlap

    private const int MinNpcCount = 4;
    private const int MaxNpcCount = 8;

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
        int worldOffsetX, int worldOffsetY, int worldZ)
    {
        var mat = GetMaterial(biome);
        int townSize = MinTownSize + rng.Next(MaxTownSize - MinTownSize + 1);
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
                    tile.Type = TileType.Floor;
                    tile.GlyphId = TileDefinitions.GlyphFloor;
                    tile.FgColor = TileDefinitions.ColorFloorFg;
                    tile.BgColor = TileDefinitions.ColorBlack;
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

        // Generate house positions (non-overlapping)
        var houses = new List<Room>();
        for (int attempt = 0; attempt < MaxHouses * 10 && houses.Count < MaxHouses; attempt++)
        {
            int w = HouseMinSize + rng.Next(HouseMaxSize - HouseMinSize + 1);
            int h = HouseMinSize + rng.Next(HouseMaxSize - HouseMinSize + 1);
            int hx = townStart + 2 + rng.Next(Math.Max(1, townSize - w - 4));
            int hy = townStart + 2 + rng.Next(Math.Max(1, townSize - h - 4));

            // Ensure the house fits within the chunk and doesn't overlap others
            if (hx + w >= townEnd - 1 || hy + h >= townEnd - 1) continue;
            bool overlaps = false;
            foreach (var existing in houses)
            {
                if (hx - GapBetweenHouses < existing.X + existing.Width && hx + w + GapBetweenHouses > existing.X &&
                    hy - GapBetweenHouses < existing.Y + existing.Height && hy + h + GapBetweenHouses > existing.Y)
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
            BuildHouse(chunk, house, rng, mat, worldOffsetX, worldOffsetY, worldZ, result);
        }

        // Place a torch in the town center
        result.Elements.Add(new DungeonElement(
            new Position(townCenterX, townCenterY, worldZ),
            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
            new LightSource(8, TileDefinitions.ColorTorchFg)));

        // Spawn town NPCs in the town area
        int npcCount = MinNpcCount + rng.Next(MaxNpcCount - MinNpcCount + 1);
        for (int i = 0; i < npcCount; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int nx = townStart + 2 + rng.Next(townSize - 4);
                int ny = townStart + 2 + rng.Next(townSize - 4);
                if (nx < 0 || nx >= Chunk.Size || ny < 0 || ny >= Chunk.Size) continue;
                if (chunk.Tiles[nx, ny].Type != TileType.Floor) continue;

                result.TownNpcs.Add((
                    new Position(worldOffsetX + nx, worldOffsetY + ny, worldZ),
                    TownNpcDefinitions.PickName(rng),
                    townCenterX, townCenterY, townSize / 2
                ));
                break;
            }
        }
    }

    private static void BuildHouse(Chunk chunk, Room house, SeededRandom rng, TownMaterial mat,
        int worldOffsetX, int worldOffsetY, int worldZ, GenerationResult result)
    {
        // Floor inside (inset by 1 for walls)
        for (int x = house.X + 1; x < house.X + house.Width - 1; x++)
        {
            for (int y = house.Y + 1; y < house.Y + house.Height - 1; y++)
            {
                if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) continue;
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Floor;
                tile.GlyphId = TileDefinitions.GlyphFloor;
                tile.FgColor = TileDefinitions.ColorFloorFg;
                tile.BgColor = TileDefinitions.ColorBlack;
                tile.PlaceableItemId = mat.FloorTileItemId;
                tile.PlaceableItemExtra = 0;
            }
        }

        // Walls — base tile is floor, placeable is the wall item
        for (int x = house.X; x < house.X + house.Width; x++)
        {
            SetPlaceable(chunk, x, house.Y, mat.WallItemId);
            SetPlaceable(chunk, x, house.Y + house.Height - 1, mat.WallItemId);
        }
        for (int y = house.Y; y < house.Y + house.Height; y++)
        {
            SetPlaceable(chunk, house.X, y, mat.WallItemId);
            SetPlaceable(chunk, house.X + house.Width - 1, y, mat.WallItemId);
        }

        // Place a door on a wall
        int doorSide = rng.Next(4); // 0=N, 1=S, 2=W, 3=E
        int doorX, doorY;
        switch (doorSide)
        {
            case 0:
                doorX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                doorY = house.Y;
                break;
            case 1:
                doorX = house.X + 1 + rng.Next(Math.Max(1, house.Width - 2));
                doorY = house.Y + house.Height - 1;
                break;
            case 2:
                doorX = house.X;
                doorY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
            default:
                doorX = house.X + house.Width - 1;
                doorY = house.Y + 1 + rng.Next(Math.Max(1, house.Height - 2));
                break;
        }
        if (doorX >= 0 && doorX < Chunk.Size && doorY >= 0 && doorY < Chunk.Size)
        {
            ref var doorTile = ref chunk.Tiles[doorX, doorY];
            doorTile.PlaceableItemId = mat.DoorItemId;
            doorTile.PlaceableItemExtra = 0; // closed
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
            windowTile.PlaceableItemId = mat.WindowItemId;
            windowTile.PlaceableItemExtra = 0;
        }

        // Place furniture inside the house
        PlaceFurniture(chunk, house, rng, result, worldOffsetX, worldOffsetY, worldZ);
    }

    private static void PlaceFurniture(Chunk chunk, Room house, SeededRandom rng,
        GenerationResult result, int worldOffsetX, int worldOffsetY, int worldZ)
    {
        int interiorX = house.X + 1;
        int interiorY = house.Y + 1;
        int interiorW = house.Width - 2;
        int interiorH = house.Height - 2;

        // Try placing a table near center
        PlaceBuildable(chunk, interiorX + interiorW / 2, interiorY + interiorH / 2, ItemDefinitions.WoodenTable);

        // Try placing chairs around the table
        if (interiorW >= 3 && interiorH >= 3)
        {
            PlaceBuildable(chunk, interiorX + interiorW / 2 - 1, interiorY + interiorH / 2, ItemDefinitions.WoodenChair);
            PlaceBuildable(chunk, interiorX + interiorW / 2 + 1, interiorY + interiorH / 2, ItemDefinitions.WoodenChair);
        }

        // Bed in a corner
        PlaceBuildable(chunk, interiorX, interiorY, ItemDefinitions.WoodenBed);

        // Bookshelf along a wall
        if (interiorW >= 3)
        {
            PlaceBuildable(chunk, interiorX + interiorW - 1, interiorY, ItemDefinitions.WoodenBookshelf);
        }

        // Torch inside the house
        int torchX = interiorX + interiorW / 2;
        int torchY = interiorY;
        if (torchX >= 0 && torchX < Chunk.Size && torchY >= 0 && torchY < Chunk.Size &&
            chunk.Tiles[torchX, torchY].Type == TileType.Floor)
        {
            result.Elements.Add(new DungeonElement(
                new Position(worldOffsetX + torchX, worldOffsetY + torchY, worldZ),
                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                new LightSource(5, TileDefinitions.ColorTorchFg)));
        }
    }

    /// <summary>
    /// Places a buildable item onto a floor tile — only sets the placeable fields.
    /// </summary>
    private static void PlaceBuildable(Chunk chunk, int x, int y, int itemTypeId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type != TileType.Floor) return;
        tile.PlaceableItemId = itemTypeId;
        tile.PlaceableItemExtra = 0;
    }

    /// <summary>
    /// Sets a tile as floor with the given placeable item.
    /// Used for structural elements (walls, doors, windows) where the base terrain must be floor.
    /// </summary>
    private static void SetPlaceable(Chunk chunk, int x, int y, int placeableItemId)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Floor;
        tile.GlyphId = TileDefinitions.GlyphFloor;
        tile.FgColor = TileDefinitions.ColorFloorFg;
        tile.BgColor = TileDefinitions.ColorBlack;
        tile.PlaceableItemId = placeableItemId;
        tile.PlaceableItemExtra = 0;
    }

    /// <summary>
    /// Construction material per biome, using actual buildable item type IDs.
    /// This ensures all town tiles can be picked up by the player.
    /// </summary>
    private static TownMaterial GetMaterial(BiomeType biome) => biome switch
    {
        BiomeType.Forest or BiomeType.Fungal => new(
            ItemDefinitions.WoodenWall, ItemDefinitions.WoodenDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.WoodenFloorTile),
        BiomeType.Ice => new(
            ItemDefinitions.IronWall, ItemDefinitions.IronDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.IronFloorTile),
        BiomeType.Lava or BiomeType.Infernal => new(
            ItemDefinitions.IronWall, ItemDefinitions.IronDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.IronFloorTile),
        BiomeType.Arcane or BiomeType.Sewer => new(
            ItemDefinitions.CopperWall, ItemDefinitions.CopperDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.CopperFloorTile),
        BiomeType.Crypt or BiomeType.Ruined => new(
            ItemDefinitions.IronWall, ItemDefinitions.IronDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.StoneFloorTile),
        _ => new(
            ItemDefinitions.IronWall, ItemDefinitions.IronDoor, ItemDefinitions.WoodenWindow, ItemDefinitions.StoneFloorTile),
    };

    private readonly record struct TownMaterial(int WallItemId, int DoorItemId, int WindowItemId, int FloorTileItemId);
}
