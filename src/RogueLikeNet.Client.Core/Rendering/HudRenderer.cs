using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the in-game HUD panel (HP, stats, skills, equipment, quick slots, floor items, controls).
/// </summary>
public sealed class HudRenderer
{
    private HudLayout _layout = CreateHudLayout();

    private static HudLayout CreateHudLayout()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "HP", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Stats", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "Skills", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "Equipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "QuickSlots", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 8, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "FloorItems", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true });
        layout.AddSection(new HudSection { Name = "Controls", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 2 });
        return layout;
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        // Vertical separator
        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 2;

        var hud = state.PlayerState;
        if (hud == null)
        {
            AsciiDraw.DrawString(r, col, 1, "No data", RenderingTheme.Dim);
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
                    AsciiDraw.DrawString(r, col, row, "HP", RenderingTheme.HpText);
                    row++;
                    if (row >= maxRow) break;
                    int barW = innerW;
                    float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
                    int filled = (int)(barW * hpRatio);
                    for (int i = 0; i < barW; i++)
                        AsciiDraw.DrawChar(r, col + i, row, i < filled ? '\u2588' : '\u2591', i < filled ? RenderingTheme.HpFill : RenderingTheme.HpBar);
                    row++;
                    if (row >= maxRow) break;
                    string hpText = $"{hud.Health}/{hud.MaxHealth}";
                    AsciiDraw.DrawString(r, col, row, hpText, RenderingTheme.HpText);
                    break;

                case "Stats":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, $"ATK: {hud.Attack}", RenderingTheme.Stats); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, $"DEF: {hud.Defense}", RenderingTheme.Stats); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, $"Lv:  {hud.Level}", RenderingTheme.Level);
                    break;

                case "Skills":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "Skills", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    for (int i = 0; i < Math.Min(hud.Skills.Length, 2) && row < maxRow; i++)
                    {
                        if (hud.Skills[i].Id == 0) continue;
                        string key = i == 0 ? "Q" : "E";
                        string name = !string.IsNullOrEmpty(hud.Skills[i].Name)
                            ? hud.Skills[i].Name : $"Skill {i + 1}";
                        int cd = hud.Skills[i].Cooldown;
                        string text = cd > 0 ? $"[{key}]{name} cd:{cd}" : $"[{key}]{name}";
                        AsciiDraw.DrawString(r, col, row, text, cd > 0 ? RenderingTheme.SkillCd : RenderingTheme.SkillReady);
                        row++;
                    }
                    break;

                case "Equipment":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "Equipment", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    {
                        string wpn = hud.EquippedWeapon != null ? AsciiDraw.ItemDisplayName(hud.EquippedWeapon.ItemTypeId, hud.EquippedWeapon.Rarity) : "---";
                        string arm = hud.EquippedArmor != null ? AsciiDraw.ItemDisplayName(hud.EquippedArmor.ItemTypeId, hud.EquippedArmor.Rarity) : "---";
                        var wpnColor = hud.EquippedWeapon != null ? AsciiDraw.RarityColor(hud.EquippedWeapon.Rarity) : RenderingTheme.Item;
                        var armColor = hud.EquippedArmor != null ? AsciiDraw.RarityColor(hud.EquippedArmor.Rarity) : RenderingTheme.Item;
                        AsciiDraw.DrawString(r, col, row, $"W: {wpn}", wpnColor); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, $"A: {arm}", armColor);
                    }
                    break;

                case "QuickSlots":
                    RenderQuickSlotsSection(r, col, innerW, row, maxRow, hud, _layout);
                    break;

                case "FloorItems":
                    RenderFloorItemsSection(r, col, innerW, row, maxRow, state);
                    break;

                case "Controls":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[I] Inventory", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Esc] Menu", RenderingTheme.Dim);
                    break;
            }
        }
    }

    private static void RenderQuickSlotsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudLayout layout)
    {
        bool focused = layout.FocusedSection?.Name == "QuickSlots";

        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, focused ? "\u25ba Quick Use Slots" : "Quick Use Slots", focused ? RenderingTheme.Selected : RenderingTheme.Title);
        row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW);
        row++;

        int[] qsIndices = hud.QuickSlotIndices;
        for (int i = 0; i < 4 && row < maxRow; i++)
        {
            int invIdx = i < qsIndices.Length ? qsIndices[i] : -1;
            if (invIdx >= 0 && invIdx < hud.InventoryItems.Length)
            {
                var item = hud.InventoryItems[invIdx];
                string name = AsciiDraw.ItemDisplayName(item.ItemTypeId, item.Rarity);
                int stack = item.StackCount;
                string stackStr = stack > 1 ? $"x{stack}" : "";
                AsciiDraw.DrawString(r, col, row, $"[{i + 1}]{name}{stackStr}", AsciiDraw.RarityColor(item.Rarity));
            }
            else
            {
                AsciiDraw.DrawString(r, col, row, $"[{i + 1}] ---", RenderingTheme.Dim);
            }
            row++;
        }

        if (row < maxRow)
            AsciiDraw.DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", RenderingTheme.Inv);
    }

    private static void RenderFloorItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        ClientGameState state)
    {
        var floorItems = state.GetFloorItems();
        if (floorItems.Length == 0) return;

        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "On Ground", RenderingTheme.Title); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
        int floorToShow = Math.Min(floorItems.Length, 4);
        for (int i = 0; i < floorToShow && row < maxRow; i++)
        {
            var (itemTypeId, rarity) = floorItems[i];
            string name = AsciiDraw.ItemDisplayName(itemTypeId, rarity);
            AsciiDraw.DrawString(r, col, row, $"  {name}", AsciiDraw.RarityColor(rarity));
            row++;
        }
        if (row < maxRow)
            AsciiDraw.DrawString(r, col, row, "[G] Pick up", RenderingTheme.Dim);
    }
}
