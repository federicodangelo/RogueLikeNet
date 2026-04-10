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
        layout.AddSection(new HudSection { Name = "InvEquipment", Anchor = HudAnchor.Top, IsFixedHeight = true, FixedHeight = 13, AcceptsInput = true });
        layout.AddSection(new HudSection { Name = "InvActions", Anchor = HudAnchor.Bottom, IsFixedHeight = true, FixedHeight = 10 });
        layout.SetFocus(1);
        return layout;
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int hudStartCol, int totalRows, bool isPlacingMode = false)
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
                        AsciiDraw.DrawString(r, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", RenderingTheme.Inv); row++;
                        row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawHudSeparator(r, col, row, innerW); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[Enter] Use / Unequip", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[E] Equip  [X] Drop", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[P] Place buildable", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[1-4] Assign slot", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[Tab] Switch section", RenderingTheme.Dim); row++;
                        if (row >= maxRow) break;
                        AsciiDraw.DrawString(r, col, row, "[Esc] Close", RenderingTheme.Dim);
                    }
                    break;
            }
        }
    }

    private static void RenderItemsSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud, HudSection section, bool focused)
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
            string name;
            if (i < hud.InventoryItems.Length)
            {
                var item = hud.InventoryItems[i];
                name = AsciiDraw.ItemDisplayName(item.ItemTypeId);
            }
            else
            {
                name = "---";
            }
            int stack = i < hud.InventoryItems.Length ? hud.InventoryItems[i].StackCount : 1;
            string stackStr = stack > 1 ? $" x{stack}" : "";
            string text = $"{prefix}{slotTag}{catTag}{name}{stackStr}";
            if (text.Length > innerW) text = text[..innerW];
            bool isQuickSlot = slotTag != "   ";
            var color = sel ? RenderingTheme.InvSel : RenderingTheme.Item;
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }

        if (needsBottomIndicator && row < maxRow)
        {
            AsciiDraw.DrawString(r, col, row, "  \u2193 more items below", RenderingTheme.Dim);
        }
    }

    private void RenderPreviewSection(ISpriteRenderer r, int col, int innerW, int row, int maxRow,
        PlayerStateMsg hud)
    {
        // Always render the section header and separator, even if no item is selected, to keep the layout consistent
        if (row >= maxRow) return;
        AsciiDraw.DrawString(r, col, row, "If equipped:", RenderingTheme.Dim); row++;
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

        // Only show preview for equippable items (weapons, armor, tools)
        if (!def.IsEquippable)
            return;

        // Resolve the correct equipment slot from JSON registry
        int targetSlot;
        if (def.EquipSlot is { } regSlot)
            targetSlot = (int)regSlot;
        else
            targetSlot = def.Category is ItemCategory.Weapon or ItemCategory.Tool
                ? (int)EquipSlot.Weapon : (int)EquipSlot.Chest;
        var equipped = Array.Find(hud.EquippedItems, e => e.EquipSlot == targetSlot);

        var eqDef = equipped != null ? GameData.Instance.Items.Get(equipped.ItemTypeId) : null;
        int eqAtk = eqDef?.BaseAttack ?? 0;
        int eqDefVal = eqDef?.BaseDefense ?? 0;
        int eqHp = eqDef?.BaseHealth ?? 0;

        int diffAtk = def.BaseAttack - eqAtk;
        int diffDef = def.BaseDefense - eqDefVal;
        int diffHp = def.BaseHealth - eqHp;


        if (row < maxRow && diffAtk != 0)
        {
            string sign = diffAtk > 0 ? "+" : "";
            var color = diffAtk > 0 ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            AsciiDraw.DrawString(r, col, row, $"  ATK: {sign}{diffAtk}", color);
            row++;
        }
        if (row < maxRow && diffDef != 0)
        {
            string sign = diffDef > 0 ? "+" : "";
            var color = diffDef > 0 ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            AsciiDraw.DrawString(r, col, row, $"  DEF: {sign}{diffDef}", color);
            row++;
        }
        if (row < maxRow && diffHp != 0)
        {
            string sign = diffHp > 0 ? "+" : "";
            var color = diffHp > 0 ? RenderingTheme.StatPositive : RenderingTheme.StatNegative;
            AsciiDraw.DrawString(r, col, row, $"  HP:  {sign}{diffHp}", color);
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

        for (int i = 0; i < Equipment.SlotCount && row < maxRow; i++)
        {
            bool sel = focused && section.SelectedIndex == i;
            string prefix = sel ? "\u25ba" : " ";
            var eqItem = Array.Find(hud.EquippedItems, e => e.EquipSlot == i);
            string name = eqItem != null ? AsciiDraw.ItemDisplayName(eqItem.ItemTypeId) : "---";
            string label = i < SlotLabels.Length ? SlotLabels[i] : "?";
            string text = $"{prefix}{label}: {name}";
            if (text.Length > innerW) text = text[..innerW];
            var color = sel ? RenderingTheme.InvSel : eqItem != null ? RenderingTheme.Item : RenderingTheme.Dim;
            AsciiDraw.DrawString(r, col, row, text, color);
            row++;
        }
    }
}
