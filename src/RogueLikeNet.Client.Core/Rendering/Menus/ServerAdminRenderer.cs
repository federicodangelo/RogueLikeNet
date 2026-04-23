using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Rendering.Menus;

public sealed class ServerAdminRenderer
{
    public void RenderServerAdmin(ISpriteRenderer r, int totalCols, int totalRows,
        SaveSlotInfoMsg[] slots, string currentSlotId, int selectedIndex, string? statusMessage, bool isError,
        bool confirmingDelete, bool creatingNew, string newSlotName, bool waiting)
    {
        int boxW = 54;
        int slotLines = slots.Length * 3;
        int boxH = Math.Max(16, slotLines + 14);
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "SERVER ADMINISTRATION", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        if (waiting)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2, "Loading...", RenderingTheme.Normal);
            return;
        }

        // Confirmation overlay
        if (confirmingDelete && selectedIndex < slots.Length)
        {
            var slot = slots[selectedIndex];
            AsciiDraw.DrawCentered(r, totalCols, by + boxH / 2 - 1, $"Delete \"{RenderingHelpers.Truncate(slot.Name, 20)}\"?", RenderingTheme.Danger);
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

        int row = sepY + 2;
        for (int i = 0; i < slots.Length; i++)
        {
            bool sel = i == selectedIndex;
            var slot = slots[i];
            string prefix = sel ? " \u25ba " : "   ";

            bool isCurrent = slot.SlotId == currentSlotId;
            string marker = isCurrent ? " \u2713" : "";
            string name = RenderingHelpers.Truncate(slot.Name, boxW - 14) + marker;
            var nameColor = isCurrent ? RenderingTheme.Floor : (sel ? RenderingTheme.Selected : RenderingTheme.SlotActive);
            AsciiDraw.DrawString(r, bx + 4, row, prefix + name, nameColor);

            string dateStr = RenderingHelpers.FormatUnixMs(slot.LastSavedAtUnixMs);
            string genName = GeneratorRegistry.GetNameOrId(slot.GeneratorId);
            string details = $"     Seed: {slot.Seed}  Gen: {genName}  Saved: {dateStr}";
            AsciiDraw.DrawString(r, bx + 4, row + 1, RenderingHelpers.Truncate(details, boxW - 6), RenderingTheme.SlotDate);

            row += 3;
        }

        // Action items
        int actionStart = row;
        string[] actions = ["+ New Game", "Back"];
        for (int i = 0; i < actions.Length; i++)
        {
            int idx = slots.Length + i;
            bool sel = idx == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            AsciiDraw.DrawString(r, bx + 4, actionStart + i, prefix + actions[i], sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        // Status message
        if (statusMessage != null)
        {
            var color = isError ? RenderingTheme.Danger : RenderingTheme.Floor;
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 4, RenderingHelpers.Truncate(statusMessage, boxW - 4), color);
        }

        string footer = selectedIndex < slots.Length
            ? "\u2191\u2193 Navigate   Enter Load   X Delete   Esc Back"
            : "\u2191\u2193 Navigate   Enter Select   Esc Back";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }
}
