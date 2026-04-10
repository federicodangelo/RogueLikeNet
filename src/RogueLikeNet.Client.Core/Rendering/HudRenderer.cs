using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the in-game HUD panel (HP, stats, skills, equipment, quick slots, floor items, controls).
/// </summary>
public sealed class HudRenderer
{
    private HudLayout _layout = CreateHudLayout();
    private int _tw;
    private int _th;
    private float _fs;

    private void Dc(ISpriteRenderer r, int tx, int ty, char ch, Color4 c) =>
        r.DrawTextScreen(tx * _tw, ty * _th, ch.ToString(), c, _fs);

    private void Ds(ISpriteRenderer r, int tx, int ty, string text, Color4 c) =>
        r.DrawTextScreen(tx * _tw, ty * _th, text, c, _fs);

    private void Sep(ISpriteRenderer r, int col, int row, int width)
    {
        for (int i = 0; i < width; i++)
            Dc(r, col + i, row, '\u2500', RenderingTheme.Dim);
    }

    private static HudLayout CreateHudLayout()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "HP", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Hunger", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 3 });
        layout.AddSection(new HudSection { Name = "Stats", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Skills", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "Equipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "QuickSlots", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 8, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "FloorItems", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true });
        layout.AddSection(new HudSection { Name = "Controls", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 5 });
        return layout;
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows,
        int tileW = 0, int tileH = 0, float fontScale = 0f, bool isPickingUpPlaced = false)
    {
        _tw = tileW > 0 ? tileW : AsciiDraw.TileWidth;
        _th = tileH > 0 ? tileH : AsciiDraw.TileHeight;
        _fs = fontScale > 0f ? fontScale : AsciiDraw.FontScale;

        float hx = hudStartCol * _tw;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * _tw, totalRows * _th, RenderingTheme.HudBg);

        // Vertical separator
        Dc(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            Dc(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        Dc(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 2;

        var hud = state.PlayerState;
        if (hud == null)
        {
            Ds(r, col, 1, "No data", RenderingTheme.Dim);
            return;
        }

        _layout.ComputeLayout(totalRows);

        foreach (var section in _layout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;

            switch (section.Name)
            {
                case "HP":
                    if (row >= maxRow) break;
                    Ds(r, col, row, "HP", RenderingTheme.HpText);
                    row++;
                    if (row >= maxRow) break;
                    int barW = innerW;
                    float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
                    int filled = (int)(barW * hpRatio);
                    for (int i = 0; i < barW; i++)
                        Dc(r, col + i, row, i < filled ? '\u2588' : '\u2591', i < filled ? RenderingTheme.HpFill : RenderingTheme.HpBar);
                    row++;
                    if (row >= maxRow) break;
                    string hpText = $"{hud.Health}/{hud.MaxHealth}";
                    Ds(r, col, row, hpText, RenderingTheme.HpText);
                    break;

                case "Hunger":
                    if (row >= maxRow) break;
                    int hungerBarW = innerW;
                    float hungerRatio = hud.MaxHunger > 0 ? (float)hud.Hunger / hud.MaxHunger : 0;
                    int hungerFilled = (int)(hungerBarW * hungerRatio);
                    var hungerFillColor = hungerRatio > 0.5f ? RenderingTheme.HungerFill
                        : hungerRatio > 0.2f ? RenderingTheme.HungerWarn : RenderingTheme.HungerCritical;
                    for (int i = 0; i < hungerBarW; i++)
                        Dc(r, col + i, row, i < hungerFilled ? '\u2588' : '\u2591', i < hungerFilled ? hungerFillColor : RenderingTheme.HpBar);
                    row++;
                    if (row >= maxRow) break;
                    Ds(r, col, row, $"Food {hud.Hunger}/{hud.MaxHunger}", hungerFillColor);
                    break;

                case "Stats":
                    if (row >= maxRow) break;
                    Ds(r, col, row, $"ATK: {hud.Attack}", RenderingTheme.Stats); row++;
                    if (row >= maxRow) break;
                    Ds(r, col, row, $"DEF: {hud.Defense}", RenderingTheme.Stats); row++;
                    if (row >= maxRow) break;
                    Ds(r, col, row, $"Lv:  {hud.Level}", RenderingTheme.Level);
                    break;

                case "Skills":
                    if (row >= maxRow) break;
                    Ds(r, col, row, "Skills", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    Sep(r, col, row, innerW); row++;
                    for (int i = 0; i < Math.Min(hud.Skills.Length, 2) && row < maxRow; i++)
                    {
                        if (hud.Skills[i].Id == 0) continue;
                        string key = i == 0 ? "Q" : "E";
                        string name = !string.IsNullOrEmpty(hud.Skills[i].Name)
                            ? hud.Skills[i].Name : $"Skill {i + 1}";
                        int cd = hud.Skills[i].Cooldown;
                        string text = cd > 0 ? $"[{key}]{name} cd:{cd}" : $"[{key}]{name}";
                        Ds(r, col, row, text, cd > 0 ? RenderingTheme.SkillCd : RenderingTheme.SkillReady);
                        row++;
                    }
                    break;

                case "Equipment":
                    if (row >= maxRow) break;
                    Ds(r, col, row, "Equipment", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    Sep(r, col, row, innerW); row++;
                    {
                        // Compact view: show all equipped items in a single summary line
                        var parts = new System.Text.StringBuilder();
                        foreach (var eq in hud.EquippedItems)
                        {
                            if (parts.Length > 0) parts.Append(' ');
                            string slotPrefix = ((EquipSlot)eq.EquipSlot) switch
                            {
                                EquipSlot.Hand => "W",
                                EquipSlot.Chest => "A",
                                EquipSlot.Head => "H",
                                EquipSlot.Legs => "L",
                                EquipSlot.Boots => "B",
                                EquipSlot.Gloves => "G",
                                EquipSlot.Offhand => "O",
                                EquipSlot.Ring => "R",
                                EquipSlot.Necklace => "N",
                                EquipSlot.Belt => "T",
                                _ => "?"
                            };
                            parts.Append($"{slotPrefix}:{AsciiDraw.ItemDisplayName(eq.ItemTypeId)}");
                        }
                        if (row < maxRow)
                        {
                            string equipText = parts.Length > 0 ? parts.ToString() : "---";
                            if (equipText.Length > innerW) equipText = equipText[..innerW];
                            Ds(r, col, row, equipText, RenderingTheme.Item);
                        }
                    }
                    break;

                case "QuickSlots":
                    RenderQuickSlotsSection(r, col, innerW, row, maxRow, hud, _layout);
                    break;

                case "FloorItems":
                    RenderFloorItemsSection(r, col, innerW, row, maxRow, state);
                    break;

                case "Controls":
                    RenderControlsSection(r, col, innerW, row, maxRow, state, isPickingUpPlaced);
                    break;
            }
        }
    }

    private void RenderQuickSlotsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudLayout layout)
    {
        bool focused = layout.FocusedSection?.Name == "QuickSlots";

        if (row >= maxRow) return;
        Ds(r, col, row, focused ? "\u25ba Quick Use Slots" : "Quick Use Slots", focused ? RenderingTheme.Selected : RenderingTheme.Title);
        row++;
        if (row >= maxRow) return;
        Sep(r, col, row, innerW);
        row++;

        int[] qsIndices = hud.QuickSlotIndices;
        for (int i = 0; i < 4 && row < maxRow; i++)
        {
            int invIdx = i < qsIndices.Length ? qsIndices[i] : -1;
            if (invIdx >= 0 && invIdx < hud.InventoryItems.Length)
            {
                var item = hud.InventoryItems[invIdx];
                string name = AsciiDraw.ItemDisplayName(item.ItemTypeId);
                int stack = item.StackCount;
                string stackStr = stack > 1 ? $"x{stack}" : "";
                Ds(r, col, row, $"[{i + 1}]{name}{stackStr}", RenderingTheme.Item);
            }
            else
            {
                Ds(r, col, row, $"[{i + 1}] ---", RenderingTheme.Dim);
            }
            row++;
        }

        if (row < maxRow)
            Ds(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", RenderingTheme.Inv);
    }

    private void RenderFloorItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        ClientGameState state)
    {
        var floorItems = state.GetFloorItems();
        if (floorItems.Length == 0) return;

        if (row >= maxRow) return;
        Ds(r, col, row, "On Ground", RenderingTheme.Title); row++;
        if (row >= maxRow) return;
        Sep(r, col, row, innerW); row++;
        int floorToShow = Math.Min(floorItems.Length, 4);
        for (int i = 0; i < floorToShow && row < maxRow; i++)
        {
            var itemTypeId = floorItems[i];
            string name = AsciiDraw.ItemDisplayName(itemTypeId);
            Ds(r, col, row, $"  {name}", RenderingTheme.RarityCommon);
            row++;
        }
        if (row < maxRow)
            Ds(r, col, row, "[G] Pick up", RenderingTheme.Dim);
    }

    private void RenderControlsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        ClientGameState state, bool isPickingUpPlaced)
    {
        if (row >= maxRow) return;
        Ds(r, col, row, "[I] Inventory", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        Ds(r, col, row, "[C] Crafting", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        Ds(r, col, row, "[Esc] Menu", RenderingTheme.Dim); row++;

        if (isPickingUpPlaced)
        {
            if (row >= maxRow) return;
            Ds(r, col, row, "Pick dir: ↑↓←→", RenderingTheme.Selected); row++;
            if (row >= maxRow) return;
            Ds(r, col, row, "[Esc] Cancel", RenderingTheme.Dim);
        }
        else if (HasAdjacentPickableTile(state))
        {
            if (row >= maxRow) return;
            Ds(r, col, row, "[P] Pick up tile", RenderingTheme.Stats);
        }
        else if (AboveStairsTile(state))
        {
            if (row >= maxRow) return;
            Ds(r, col, row, "[>] Use stairs", RenderingTheme.Stats);
        }
    }

    private static bool HasAdjacentPickableTile(ClientGameState state)
    {
        int px = state.PlayerX, py = state.PlayerY;
        ReadOnlySpan<(int, int)> offsets = [(0, -1), (0, 1), (-1, 0), (1, 0)];
        foreach (var (dx, dy) in offsets)
        {
            var tile = state.GetTile(px + dx, py + dy);
            if (tile.PlaceableItemId != 0)
                return true;
        }
        return false;
    }

    private static bool AboveStairsTile(ClientGameState state)
    {
        var tile = state.GetTile(state.PlayerX, state.PlayerY);
        return tile.Type == TileType.StairsUp || tile.Type == TileType.StairsDown;
    }
}
