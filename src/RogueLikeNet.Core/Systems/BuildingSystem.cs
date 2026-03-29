using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes building placement: validates that the target tile is adjacent floor,
/// removes the buildable item from inventory, and modifies the world tile.
/// </summary>
public class BuildingSystem
{
    // Cardinal directions for adjacency check
    private static readonly (int DX, int DY)[] AdjacentOffsets =
        [(0, -1), (0, 1), (-1, 0), (1, 0)];

    public void Update(Arch.Core.World world, WorldMap map)
    {
        ProcessPlacement(world, map);
        ProcessPickUpPlaced(world, map);
    }

    private void ProcessPlacement(Arch.Core.World world, WorldMap map)
    {
        var actions = new List<(Entity Player, int Slot, int TargetX, int TargetY)>();

        var query = new QueryDescription().WithAll<PlayerInput, Inventory, Position>();
        world.Query(in query, (Entity player, ref PlayerInput input, ref Position pos) =>
        {
            if (input.ActionType != ActionTypes.PlaceItem) return;

            int targetX = pos.X + input.TargetX;
            int targetY = pos.Y + input.TargetY;
            actions.Add((player, input.ItemSlot, targetX, targetY));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, slot, targetX, targetY) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            ref var pos = ref world.Get<Position>(player);
            if (inv.Items == null || slot < 0 || slot >= inv.Items.Count) continue;

            var itemData = inv.Items[slot];
            var def = ItemDefinitions.Get(itemData.ItemTypeId);
            if (def.Category != ItemDefinitions.CategoryBuildable) continue;

            // Validate adjacency (target must be within 1 cardinal tile of player)
            int dx = targetX - pos.X;
            int dy = targetY - pos.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in AdjacentOffsets)
            {
                if (dx == ox && dy == oy) { adjacent = true; break; }
            }
            if (!adjacent) continue;

            // Target tile must be a floor tile
            var tile = map.GetTile(targetX, targetY);
            if (tile.Type != TileType.Floor) continue;

            // Check no entity occupies the target position
            bool occupied = false;
            var entityQuery = new QueryDescription().WithAll<Position, Health>();
            world.Query(in entityQuery, (ref Position ePos) =>
            {
                if (ePos.X == targetX && ePos.Y == targetY)
                    occupied = true;
            });
            if (occupied) continue;

            // Determine what tile to place
            var (tileType, glyphId, fgColor) = GetBuildableTile(itemData.ItemTypeId);

            // Modify the world tile
            map.SetTile(targetX, targetY, new TileInfo
            {
                Type = tileType,
                GlyphId = glyphId,
                FgColor = fgColor,
                BgColor = TileDefinitions.ColorBlack,
            });

            // Remove item from inventory (decrease stack or remove)
            var item = inv.Items[slot];
            item.StackCount--;
            if (item.StackCount <= 0)
            {
                inv.Items.RemoveAt(slot);
                if (world.Has<QuickSlots>(player))
                {
                    ref var qs = ref world.Get<QuickSlots>(player);
                    qs.OnItemRemoved(slot);
                }
            }
            else
            {
                inv.Items[slot] = item;
            }
        }
    }

    private static (TileType Type, int GlyphId, int FgColor) GetBuildableTile(int itemTypeId) => itemTypeId switch
    {
        ItemDefinitions.WoodenDoor => (TileType.DoorClosed, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorWoodFg),
        ItemDefinitions.WoodenWall => (TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorWoodFg),
        ItemDefinitions.WoodenWindow => (TileType.Window, TileDefinitions.GlyphWindow, TileDefinitions.ColorWindowFg),
        ItemDefinitions.CopperDoor => (TileType.DoorClosed, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorCopperFg),
        ItemDefinitions.CopperWall => (TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorCopperFg),
        ItemDefinitions.IronDoor => (TileType.DoorClosed, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorIronFg),
        ItemDefinitions.IronWall => (TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorIronFg),
        ItemDefinitions.GoldDoor => (TileType.DoorClosed, TileDefinitions.GlyphDoorClosed, TileDefinitions.ColorGoldFg),
        ItemDefinitions.GoldWall => (TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorGoldFg),
        _ => (TileType.Wall, TileDefinitions.GlyphWall, TileDefinitions.ColorWallFg),
    };

    private void ProcessPickUpPlaced(Arch.Core.World world, WorldMap map)
    {
        var actions = new List<(Entity Player, int TargetX, int TargetY)>();

        var query = new QueryDescription().WithAll<PlayerInput, Inventory, Position>();
        world.Query(in query, (Entity player, ref PlayerInput input, ref Position pos) =>
        {
            if (input.ActionType != ActionTypes.PickUpPlaced) return;

            int targetX = pos.X + input.TargetX;
            int targetY = pos.Y + input.TargetY;
            actions.Add((player, targetX, targetY));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, targetX, targetY) in actions)
        {
            if (!world.IsAlive(player)) continue;
            ref var inv = ref world.Get<Inventory>(player);
            if (inv.Items == null || inv.IsFull) continue;

            var tile = map.GetTile(targetX, targetY);
            int? itemTypeId = GetItemFromTile(tile);
            if (itemTypeId == null) continue;

            // Revert tile to floor
            map.SetTile(targetX, targetY, new TileInfo
            {
                Type = TileType.Floor,
                GlyphId = TileDefinitions.GlyphFloor,
                FgColor = TileDefinitions.ColorFloorFg,
                BgColor = TileDefinitions.ColorBlack,
            });

            // Add item to inventory (auto-stack)
            var itemData = new ItemData
            {
                ItemTypeId = itemTypeId.Value,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = 1,
            };
            var def = ItemDefinitions.Get(itemTypeId.Value);
            bool stacked = false;
            if (def.Stackable)
            {
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    if (inv.Items[i].ItemTypeId == itemData.ItemTypeId &&
                        inv.Items[i].Rarity == itemData.Rarity &&
                        inv.Items[i].StackCount < def.MaxStackSize)
                    {
                        var existing = inv.Items[i];
                        existing.StackCount++;
                        inv.Items[i] = existing;
                        stacked = true;
                        break;
                    }
                }
            }
            if (!stacked && !inv.IsFull)
                inv.Items.Add(itemData);
        }
    }

    /// <summary>
    /// Returns the buildable item type for a placed tile, or null if the tile is not a player-placed buildable.
    /// Matches by tile type, glyph, and foreground color.
    /// </summary>
    public static int? GetItemFromTile(TileInfo tile)
    {
        foreach (var buildable in ItemDefinitions.All)
        {
            if (buildable.Category != ItemDefinitions.CategoryBuildable) continue;
            var (tileType, _, fgColor) = GetBuildableTile(buildable.TypeId);
            // For doors, match both open (Door) and closed (DoorClosed) states
            bool typeMatch = tile.Type == tileType
                || (tileType == TileType.DoorClosed && tile.Type == TileType.Door);
            // For doors, accept any door glyph (vertical, horizontal, or legacy)
            bool glyphMatch;
            if (tileType == TileType.DoorClosed)
                glyphMatch = IsDoorGlyph(tile.GlyphId);
            else
            {
                var (_, expectedGlyph, _) = GetBuildableTile(buildable.TypeId);
                glyphMatch = tile.GlyphId == expectedGlyph;
            }
            if (typeMatch && glyphMatch && tile.FgColor == fgColor)
                return buildable.TypeId;
        }
        return null;
    }

    private static bool IsDoorGlyph(int glyphId) =>
        glyphId == TileDefinitions.GlyphDoorVertical
        || glyphId == TileDefinitions.GlyphDoorHorizontal
        || glyphId == TileDefinitions.GlyphDoor
        || glyphId == TileDefinitions.GlyphDoorClosed;
}
