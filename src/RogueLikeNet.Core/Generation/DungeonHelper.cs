using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Shared helpers used by all dungeon generator implementations.
/// Handles tile carving, feature placement, population, decorations, liquids, and biome tinting.
/// </summary>
internal static class DungeonHelper
{
    public static void FillWalls(Chunk chunk)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.Type = TileType.Wall;
                tile.GlyphId = TileDefinitions.GlyphWall;
                tile.FgColor = TileDefinitions.ColorWallFg;
                tile.BgColor = TileDefinitions.ColorBlack;
            }
    }

    public static void CarveTile(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        if (tile.Type == TileType.Wall)
        {
            tile.Type = TileType.Floor;
            tile.GlyphId = TileDefinitions.GlyphFloor;
            tile.FgColor = TileDefinitions.ColorFloorFg;
            tile.BgColor = TileDefinitions.ColorBlack;
        }
    }

    public static void CarveFloor(Chunk chunk, int x, int y)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = TileType.Floor;
        tile.GlyphId = TileDefinitions.GlyphFloor;
        tile.FgColor = TileDefinitions.ColorFloorFg;
        tile.BgColor = TileDefinitions.ColorBlack;
    }

    public static void CarveRoom(Chunk chunk, Room room)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
            for (int y = room.Y; y < room.Y + room.Height; y++)
                CarveFloor(chunk, x, y);
    }

    public static void PlaceFeature(Chunk chunk, int x, int y, TileType type, int glyph, int fgColor)
    {
        if (x < 0 || x >= Chunk.Size || y < 0 || y >= Chunk.Size) return;
        ref var tile = ref chunk.Tiles[x, y];
        tile.Type = type;
        tile.GlyphId = glyph;
        tile.FgColor = fgColor;
    }

    public static void PlaceStairs(Chunk chunk, List<Room> rooms)
    {
        if (rooms.Count < 2) return;
        var first = rooms[0];
        var last = rooms[^1];
        PlaceFeature(chunk, first.CenterX, first.CenterY, TileType.StairsUp,
            TileDefinitions.GlyphStairsUp, TileDefinitions.ColorWhite);
        PlaceFeature(chunk, last.CenterX, last.CenterY, TileType.StairsDown,
            TileDefinitions.GlyphStairsDown, TileDefinitions.ColorWhite);
    }

    public static void PlaceLiquidPools(Chunk chunk, List<Room> rooms, BiomeType biome, SeededRandom rng)
    {
        var liquidDef = BiomeDefinitions.GetLiquid(biome);
        if (liquidDef == null) return;
        var liq = liquidDef.Value;

        for (int i = 1; i < rooms.Count - 1; i++)
        {
            if (rng.Next(100) >= liq.RoomChance) continue;
            var room = rooms[i];
            if (room.Width < 6 || room.Height < 6) continue;

            int poolX = room.X + 2;
            int poolY = room.Y + 2;
            int poolW = room.Width - 4;
            int poolH = room.Height - 4;

            for (int x = poolX; x < poolX + poolW; x++)
                for (int y = poolY; y < poolY + poolH; y++)
                {
                    if (x >= 0 && x < Chunk.Size && y >= 0 && y < Chunk.Size)
                    {
                        ref var tile = ref chunk.Tiles[x, y];
                        tile.Type = liq.Type;
                        tile.GlyphId = liq.GlyphId;
                        tile.FgColor = liq.FgColor;
                        tile.BgColor = liq.BgColor;
                    }
                }
        }
    }

    public static void PlaceDecorations(Chunk chunk, BiomeType biome, SeededRandom rng)
    {
        var decorations = BiomeDefinitions.GetDecorations(biome);
        if (decorations.Length == 0) return;

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                if (tile.Type != TileType.Floor) continue;

                foreach (var deco in decorations)
                {
                    if (rng.Next(100) < deco.Chance)
                    {
                        tile.Type = TileType.Decoration;
                        tile.GlyphId = deco.GlyphId;
                        tile.FgColor = deco.FgColor;
                        break;
                    }
                }
            }
    }

    public static void PopulateRoom(Room room, SeededRandom rng, GenerationResult result, int difficulty, int worldOffsetX, int worldOffsetY)
    {
        int monsterCount = 1 + rng.Next(3);
        for (int m = 0; m < monsterCount; m++)
        {
            int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
            int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
            var def = NpcDefinitions.Pick(rng, difficulty);
            int hpScale = 1 + difficulty / 2;
            result.Monsters.Add((new Position(worldOffsetX + x, worldOffsetY + y), new MonsterData
            {
                MonsterTypeId = def.TypeId,
                Health = def.Health * hpScale,
                Attack = def.Attack + difficulty,
                Defense = def.Defense + difficulty / 2,
                Speed = def.Speed,
            }));
        }

        if (rng.Next(100) < 30)
        {
            int x = room.X + 1 + rng.Next(Math.Max(1, room.Width - 2));
            int y = room.Y + 1 + rng.Next(Math.Max(1, room.Height - 2));
            var loot = ItemDefinitions.GenerateLoot(rng, difficulty);
            int rarityMult = 100 + loot.Rarity * 50;
            result.Items.Add((new Position(worldOffsetX + x, worldOffsetY + y), new ItemData
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

        if (rng.Next(100) < 40)
        {
            result.Elements.Add(new DungeonElement(
                new Position(worldOffsetX + room.CenterX, worldOffsetY + room.CenterY),
                new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
                new LightSource(6, TileDefinitions.ColorTorchFg)));
        }
    }

    public static void PopulateRooms(List<Room> rooms, SeededRandom rng, GenerationResult result, int difficulty, int worldOffsetX, int worldOffsetY)
    {
        for (int i = 1; i < rooms.Count; i++)
            PopulateRoom(rooms[i], rng, result, difficulty, worldOffsetX, worldOffsetY);
    }

    public static void ApplyBiomeTint(Chunk chunk, BiomeType biome)
    {
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                tile.FgColor = BiomeDefinitions.ApplyBiomeTint(tile.FgColor, biome);
                tile.BgColor = BiomeDefinitions.ApplyBiomeTint(tile.BgColor, biome);
            }
    }

    public static void CarveHLine(Chunk chunk, int x1, int x2, int y)
    {
        int minX = Math.Min(x1, x2);
        int maxX = Math.Max(x1, x2);
        for (int x = minX; x <= maxX; x++)
            CarveTile(chunk, x, y);
    }

    public static void CarveVLine(Chunk chunk, int y1, int y2, int x)
    {
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);
        for (int y = minY; y <= maxY; y++)
            CarveTile(chunk, x, y);
    }

    public static void CarveCorridor(Chunk chunk, int x1, int y1, int x2, int y2, SeededRandom rng)
    {
        if (rng.Next(2) == 0)
        {
            CarveHLine(chunk, x1, x2, y1);
            CarveVLine(chunk, y1, y2, x2);
        }
        else
        {
            CarveVLine(chunk, y1, y2, x1);
            CarveHLine(chunk, x1, x2, y2);
        }
    }
}

/// <summary>Axis-aligned room within a chunk.</summary>
internal class Room
{
    public int X, Y, Width, Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public Room(int x, int y, int w, int h)
    {
        X = x; Y = y; Width = w; Height = h;
    }
}
