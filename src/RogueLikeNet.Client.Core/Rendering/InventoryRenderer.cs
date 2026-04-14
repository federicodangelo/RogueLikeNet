using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Protocol.Messages;

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
        layout.AddSection(new HudSection { Name = "InvPreview", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 5 });
        layout.AddSection(new HudSection { Name = "InvEquipment", Anchor = HudAnchor.Top, IsFixedHeight = false, Scrollable = true, AcceptsInput = true, UseScrollIndicators = true });
        layout.AddSection(new HudSection { Name = "InvActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 4 });
        layout.SetFocus(1);
        return layout;
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows, bool isPlacingMode, ItemDefinition? selectedItemDef)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        // Vertical separator
        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);

        int col = hudStartCol + 1;
        int innerW = AsciiDraw.HudColumns - 1;

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
                    AsciiDraw.DrawString(r, col, row, $"INVENTORY {hud.InventoryCount}/{hud.InventoryCapacity}", RenderingTheme.Title);
                    AsciiDraw.DrawString(r, col + innerW - 5, row, "[Esc]", RenderingTheme.Dim);
                    row++;
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

                case "InvPreview":
                    RenderPreviewSection(r, col, innerW, row, maxRow, hud);
                    break;

                case "InvEquipment":
                    RenderEquipmentSection(r, col, innerW, row, maxRow, hud, section, focused);
                    break;

                case "InvActions":
                    if (isPlacingMode)
                    {
                        if (row >= maxRow) break;
                        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "PLACE: pick direction", RenderingTheme.Title); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[\u2190\u2191\u2192\u2193] Direction", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[Esc] Cancel", RenderingTheme.Dim);
                    }
                    else
                    {
                        if (row >= maxRow) break;
                        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                        string? focusedName = InventoryLayout.FocusedSection?.Name;
                        if (focusedName == "InvEquipment")
                        {
                            if (row >= maxRow) break;
                            AsciiDraw.DrawString(r, col, row, "[Enter] Unequip [X] Drop", RenderingTheme.Dim); row++;
                            if (row >= maxRow) break;
                            AsciiDraw.DrawString(r, col, row, "[Tab] Inventory", RenderingTheme.Dim);
                        }
                        else
                        {
                            if (row >= maxRow) break;
                            if (selectedItemDef != null)
                            {
                                if (selectedItemDef.IsEquippable)
                                    AsciiDraw.DrawString(r, col, row, "[Enter] Equip [X] Drop", RenderingTheme.Dim);
                                else if (selectedItemDef.IsConsumable)
                                    AsciiDraw.DrawString(r, col, row, "[Enter] Use [X] Drop", RenderingTheme.Dim);
                                else if (selectedItemDef.IsPlaceable)
                                    AsciiDraw.DrawString(r, col, row, "[P] Place [X] Drop", RenderingTheme.Dim);
                                else
                                    AsciiDraw.DrawString(r, col, row, "[X] Drop", RenderingTheme.Dim);
                            }
                            else
                            {
                                AsciiDraw.DrawString(r, col, row, "[X] Drop", RenderingTheme.Dim);
                            }
                            row++;
                            if (row >= maxRow) break;
                            AsciiDraw.DrawString(r, col, row, "[1-8] Slot  [Tab] Equipment", RenderingTheme.Dim);
                        }
                    }
                    break;
            }
        }
    }

    private static void RenderItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section, bool focused)
    {
        int itemCount = hud.InventoryItems.Length;
        if (itemCount == 0)
        {
            if (row < maxRow)
                AsciiDraw.DrawString(r, col, row, " (empty)", RenderingTheme.Dim);
            return;
        }
        int visibleRows = maxRow - row;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;
        int[] qsIndices = hud.QuickSlotIndices;

        // Scroll arrows at right edge instead of text lines
        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < itemCount;

        int renderEnd = Math.Min(scrollOffset + visibleRows, itemCount);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            bool sel = focused && i == selectedIndex;
            string prefix = sel ? "\u25ba" : " ";

            string slotTag = "   ";
            for (int q = 0; q < qsIndices.Length; q++)
            {
                if (qsIndices[q] == i) { slotTag = $"[{q + 1}]"; break; }
            }

            var item = hud.InventoryItems[i];
            string catTag = AsciiDraw.CategoryTag(item.Category);
            string name = AsciiDraw.ItemDisplayName(item.ItemTypeId);
            int stack = item.StackCount;
            string stackStr = stack > 1 ? $" x{stack}" : "";
            string text = $"{prefix}{slotTag}{catTag}{name}{stackStr}";
            if (text.Length > innerW) text = text[..innerW];
            var color = sel ? RenderingTheme.InvSel : RenderingTheme.Item;
            AsciiDraw.DrawString(r, col, row, text, color);

            // Draw scroll arrows at top-right / bottom-right
            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }
    }

    private void RenderPreviewSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud)
    {
        // Always render the section header and separator, even if no item is selected, to keep the layout consistent
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "If equipped / consumed:", RenderingTheme.Dim); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        // Show stat change preview for the selected inventory item
        var itemsSection = InventoryLayout.Sections.FirstOrDefault(s => s.Name == "InvItems");
        if (itemsSection == null || InventoryLayout.FocusedSection?.Name != "InvItems") return;

        int selIdx = itemsSection.SelectedIndex;
        if (selIdx < 0 || selIdx >= hud.InventoryItems.Length) return;

        var item = hud.InventoryItems[selIdx];
        var def = GameData.Instance.Items.Get(item.ItemTypeId);
        if (def == null) return;

        var relativeStats = AsciiDraw.ItemRelativeStats(def, hud);

        foreach (var stat in relativeStats)
        {
            if (row >= maxRow) break;
            AsciiDraw.DrawString(r, col, row, $"  {stat.text}", stat.color);
            row++;
        }
    }


    private static readonly string[] SlotLabels =
        ["Head", "Chest", "Legs", "Boots", "Gloves", "Weapon", "Offhand", "Ring", "Neck", "Belt"];

    private static void RenderEquipmentSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section, bool focused)
    {
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "Equipment", RenderingTheme.Title); row++;
        if (row >= maxRow) return;
        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;

        int totalSlots = Equipment.SlotCount;
        int visibleRows = maxRow - row;
        int scrollOffset = section.ScrollOffset;
        int selectedIndex = section.SelectedIndex;

        // Show scroll indicators using arrows at right edge
        bool showTopArrow = scrollOffset > 0;
        bool showBottomArrow = scrollOffset + visibleRows < totalSlots;
        if (showTopArrow && showBottomArrow && visibleRows >= 2) visibleRows -= 0; // arrows overlay, no row cost
        // Just render inline with items

        int renderEnd = Math.Min(scrollOffset + visibleRows, totalSlots);
        for (int i = scrollOffset; i < renderEnd && row < maxRow; i++)
        {
            bool sel = focused && selectedIndex == i;
            string prefix = sel ? "\u25ba" : " ";
            var eqItem = Array.Find(hud.EquippedItems, e => e.EquipSlot == i);
            string name = eqItem != null ? AsciiDraw.ItemDisplayName(eqItem.ItemTypeId) : "---";
            string label = i < SlotLabels.Length ? SlotLabels[i] : "?";
            string text = $"{prefix}{label}: {name}";
            if (text.Length > innerW) text = text[..innerW];
            var color = sel ? RenderingTheme.InvSel : eqItem != null ? RenderingTheme.Item : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, col, row, text, color);

            // Draw scroll arrows at top-right / bottom-right of equipment area
            if (i == scrollOffset && showTopArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2191', RenderingTheme.Dim);
            if (i == renderEnd - 1 && showBottomArrow)
                AsciiDraw.DrawChar(r, col + innerW - 1, row, '\u2193', RenderingTheme.Dim);
            row++;
        }
    }
}
