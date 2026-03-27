using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the inventory panel (header, item list, equipment slots, actions).
/// </summary>
public sealed class InventoryRenderer
{
    public HudLayout InventoryLayout { get; } = CreateInventoryLayout();

    private static HudLayout CreateInventoryLayout()
    {
        var layout = new HudLayout();
        layout.AddSection(new HudSection { Name = "InvHeader", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 4 });
        layout.AddSection(new HudSection { Name = "InvItems", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true });
        layout.AddSection(new HudSection { Name = "InvEquipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "InvActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 9 });
        layout.SetFocus(1);
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

        InventoryLayout.ComputeLayout(totalRows);

        foreach (var section in InventoryLayout.Sections)
        {
            int row = section.StartRow;
            int maxRow = section.StartRow + section.RowCount;
            bool focused = InventoryLayout.FocusedSection == section;

            switch (section.Name)
            {
                case "InvHeader":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "INVENTORY", RenderingTheme.Title); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    string hpStr = $"HP:{hud.Health}/{hud.MaxHealth}";
                    string atkStr = $"ATK:{hud.Attack}";
                    string defStr = $"DEF:{hud.Defense}";
                    AsciiDraw.DrawString(r, col, row, hpStr, RenderingTheme.HpText);
                    AsciiDraw.DrawString(r, col + hpStr.Length + 1, row, atkStr, RenderingTheme.Stats);
                    AsciiDraw.DrawString(r, col + hpStr.Length + 1 + atkStr.Length + 1, row, defStr, RenderingTheme.Stats);
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW);
                    break;

                case "InvItems":
                    RenderItemsSection(r, col, innerW, row, maxRow, hud, section, focused);
                    break;

                case "InvEquipment":
                    RenderEquipmentSection(r, col, innerW, row, maxRow, hud, section, focused);
                    break;

                case "InvActions":
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", RenderingTheme.Inv); row++;
                    row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Enter] Use / Unequip", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[E] Equip  [X] Drop", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[1-4] Assign slot", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Tab] Switch section", RenderingTheme.Dim); row++;
                    if (row >= maxRow) break;
                    AsciiDraw.DrawString(r, col, row, "[Esc] Close", RenderingTheme.Dim);
                    break;
            }
        }
    }

    private static void RenderItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudSection section, bool focused)
    {
        int cap = Math.Max(hud.InventoryCapacity, 4);
        int visibleRows = section.RowCount;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int[] qsIndices = hud.QuickSlotIndices;

        if (scrollOffset > 0 && row < maxRow)
        {
            AsciiDraw.DrawString(r, col, row, "  \u2191 more items above", RenderingTheme.Dim);
            row++; visibleRows--;
        }

        bool needsBottomIndicator = scrollOffset + visibleRows < cap;
        int renderRows = needsBottomIndicator ? visibleRows - 1 : visibleRows;

        int visibleEnd = Math.Min(scrollOffset + renderRows, cap);
        for (int i = scrollOffset; i < visibleEnd && row < maxRow; i++)
        {
            bool sel = focused && i == selectedIndex;
            string prefix = sel ? "\u25ba" : " ";

            string slotTag = "   ";
            for (int q = 0; q < Math.Min(qsIndices.Length, 4); q++)
            {
                if (qsIndices[q] == i) { slotTag = $"[{q + 1}]"; break; }
            }

            string catTag = i < hud.InventoryItems.Length
                ? AsciiDraw.CategoryTag(hud.InventoryItems[i].Category) : "     ";
            string name = i < hud.InventoryItems.Length && !string.IsNullOrEmpty(hud.InventoryItems[i].Name)
                ? hud.InventoryItems[i].Name : "---";
            int stack = i < hud.InventoryItems.Length ? hud.InventoryItems[i].StackCount : 1;
            string stackStr = stack > 1 ? $" x{stack}" : "";
            string text = $"{prefix}{slotTag}{catTag}{name}{stackStr}";
            if (text.Length > innerW) text = text[..innerW];
            bool isQuickSlot = slotTag != "   ";
            var color = sel ? RenderingTheme.InvSel : isQuickSlot ? RenderingTheme.Item : RenderingTheme.Inv;
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }

        if (needsBottomIndicator && row < maxRow)
        {
            AsciiDraw.DrawString(r, col, row, "  \u2193 more items below", RenderingTheme.Dim);
        }
    }

    private static void RenderEquipmentSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        Protocol.Messages.PlayerStateMsg hud, HudSection section, bool focused)
    {
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "Equipment", RenderingTheme.Title); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        if (row < maxRow)
        {
            bool sel = focused && section.SelectedIndex == 0;
            string prefix = sel ? "\u25ba" : " ";
            string wpn = !string.IsNullOrEmpty(hud.EquippedWeaponName) ? hud.EquippedWeaponName : "---";
            string text = $"{prefix}[W][Wpn]{wpn}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, sel ? RenderingTheme.InvSel : RenderingTheme.Item);
            row++;
        }
        if (row < maxRow)
        {
            bool sel = focused && section.SelectedIndex == 1;
            string prefix = sel ? "\u25ba" : " ";
            string arm = !string.IsNullOrEmpty(hud.EquippedArmorName) ? hud.EquippedArmorName : "---";
            string text = $"{prefix}[A][Arm]{arm}";
            if (text.Length > innerW) text = text[..innerW];
            AsciiDraw.DrawString(r, col, row, text, sel ? RenderingTheme.InvSel : RenderingTheme.Item);
        }
    }
}
