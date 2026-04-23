using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering;

static public class SaveSlotMenuRenderer
{

    // ── Save Slot Screen ────────────────────────────────────

    private const int SaveSlotHeightRows = 4;
    private const int SaveSlotNewGameHeightRows = 2;

    private const int SaveSlotScreenMaxHeightRows = 26;

    private const int SaveSlotScreenHeaderRows = 2;
    private const int SaveSlotScreenFooterRows = 2;

    private const int SaveSlotScreenTopBottomPaddingRows = 2;

    private const int MoreAboveOrBelowIndicatorRows = 1;

    static private int CalculateSaveSlotScreenHeight(int totalRows, int slotCount)
    {
        int maxBoxH = Math.Min(totalRows - 2, SaveSlotScreenMaxHeightRows);
        int boxH = Math.Max(
            SaveSlotHeightRows + SaveSlotScreenHeaderRows + SaveSlotScreenFooterRows + SaveSlotScreenTopBottomPaddingRows * 2 + MoreAboveOrBelowIndicatorRows * 2, // Minimum height to show at least one slot and the "more above/below" indicators
            Math.Min(
                slotCount * SaveSlotHeightRows + SaveSlotNewGameHeightRows + SaveSlotScreenHeaderRows + SaveSlotScreenFooterRows + SaveSlotScreenTopBottomPaddingRows * 2,
                maxBoxH
            )
        );
        return boxH;
    }

    static private int CalculateSaveSlotScreenContentHeight(int totalRows, int slotCount)
    {
        int boxH = CalculateSaveSlotScreenHeight(totalRows, slotCount);
        return boxH - SaveSlotScreenHeaderRows - SaveSlotScreenFooterRows - SaveSlotScreenTopBottomPaddingRows * 2;
    }

    static public int EnsureSaveSlotContentVisible(int totalRows, int slotCount, int scrollOffset, int selectedIndex)
    {
        if (scrollOffset >= selectedIndex)
            return selectedIndex;

        int availableContentRows = CalculateSaveSlotScreenAvailableContentHeight(totalRows, slotCount, scrollOffset, out var _, out var _);

        for (var i = scrollOffset; i < slotCount + 1; i++)
        {
            int rowHeight = i == 0 ? SaveSlotNewGameHeightRows : SaveSlotHeightRows;
            availableContentRows -= rowHeight;

            if (availableContentRows < 0)
            {
                scrollOffset++;
                return EnsureSaveSlotContentVisible(totalRows, slotCount, scrollOffset, selectedIndex);
            }

            if (i == selectedIndex)
                break;
        }

        return scrollOffset;
    }

    static public int CalculateSaveSlotScreenAvailableContentHeight(int totalRows, int slotCount, int scrollOffset, out bool showTopIndicator, out bool showBottomIndicator)
    {
        int contentRows = CalculateSaveSlotScreenContentHeight(totalRows, slotCount);

        int rowsNeeded =
            (scrollOffset == 0 ? SaveSlotNewGameHeightRows : 0) + // "New Game" entry only if we're at the top
            (slotCount - scrollOffset) * SaveSlotHeightRows;

        showTopIndicator = scrollOffset > 0;
        int topRows = showTopIndicator ? 1 : 0;
        showBottomIndicator = rowsNeeded > contentRows - topRows;
        int bottomRows = showBottomIndicator ? 1 : 0;
        int availableContentRows = contentRows - topRows - bottomRows;

        return availableContentRows;

    }

    public static void RenderSaveSlotScreen(ISpriteRenderer r, int totalCols, int totalRows,
        SaveSlotInfoMsg[] slots, SaveSlotInfoMsg? selectedSlot, int scrollOffset, string? statusMessage, bool isError,
        bool confirmingDelete, bool creatingNew, string newSlotName, bool waiting)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 52;
        int boxH = CalculateSaveSlotScreenHeight(totalRows, slots.Length);
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "SAVE SLOTS", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        if (waiting)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2, "Loading...", RenderingTheme.Normal);
            return;
        }

        // Confirmation overlay
        if (confirmingDelete && selectedSlot != null)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, $"Delete \"{RenderingHelpers.Truncate(selectedSlot.Name, 20)}\"?", RenderingTheme.Danger);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        // New slot name editing overlay
        if (creatingNew)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, "Enter world name:", RenderingTheme.Normal);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 1, newSlotName + "_", RenderingTheme.Selected);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 + 3, "Enter Confirm   Esc Cancel", RenderingTheme.Dim);
            return;
        }

        // Content area: rows from (sepY + 2) to (by + boxH - 5) inclusive = boxH - 8 rows
        int contentStart = by + SaveSlotScreenTopBottomPaddingRows + SaveSlotScreenHeaderRows;
        int availableContentRows = CalculateSaveSlotScreenAvailableContentHeight(totalRows, slots.Length, scrollOffset, out var showTopIndicator, out var showBottomIndicator);

        int row = contentStart;
        if (showTopIndicator)
        {
            AsciiDraw.DrawChar(r, bx + boxW - 3, row, '\u2191', RenderingTheme.Dim);
            row++;
        }

        // "New Game" action item — only if it's in the viewport and there's room
        int contentRow = 0;
        if (scrollOffset == 0)
        {
            bool sel = selectedSlot == null;
            string prefix = sel ? " \u25ba " : "   ";
            AsciiDraw.DrawString(r, bx + 4, row, prefix + "+ New Game", sel ? RenderingTheme.Selected : RenderingTheme.Normal);
            row += SaveSlotNewGameHeightRows;
            contentRow += SaveSlotNewGameHeightRows;
        }

        var fromSlot = Math.Max(scrollOffset - 1, 0); // -1 because the first visible slot is "new game", not an actual slot

        for (int i = fromSlot; i < slots.Length; i++)
        {
            if (contentRow + SaveSlotHeightRows > availableContentRows) break;

            var slot = slots[i];
            bool sel = slot == selectedSlot;
            string prefix = sel ? " \u25ba " : "   ";

            string name = RenderingHelpers.Truncate(slot.Name, boxW - 12);
            AsciiDraw.DrawString(r, bx + 4, row, prefix + name, sel ? RenderingTheme.Selected : RenderingTheme.SlotActive);

            string dateStr = RenderingHelpers.FormatUnixMs(slot.LastSavedAtUnixMs);
            string genName = GeneratorRegistry.GetNameOrId(slot.GeneratorId);
            string details1 = $"     Seed: {slot.Seed}  Gen: {genName}";
            string details2 = $"     Saved: {dateStr}";
            AsciiDraw.DrawString(r, bx + 4, row + 1, RenderingHelpers.Truncate(details1, boxW - 6), RenderingTheme.SlotDate);
            AsciiDraw.DrawString(r, bx + 4, row + 2, RenderingHelpers.Truncate(details2, boxW - 6), RenderingTheme.SlotDate);

            row += SaveSlotHeightRows;
            contentRow += SaveSlotHeightRows;
        }

        if (showBottomIndicator)
        {
            AsciiDraw.DrawChar(r, bx + boxW - 3, contentStart + (showTopIndicator ? 1 : 0) + availableContentRows, '\u2193', RenderingTheme.Dim);
        }

        // Status message
        if (statusMessage != null)
        {
            var color = isError ? RenderingTheme.Danger : RenderingTheme.Floor;
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 4, RenderingHelpers.Truncate(statusMessage, boxW - 4), color);
        }

        string footer = selectedSlot != null
            ? "\u2191\u2193 Navigate   Enter Load   X Delete   Esc Back"
            : "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }
}
