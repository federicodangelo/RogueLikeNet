using Engine.Core;
using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering.Menus;

public sealed class OptionsRenderer
{
    public void RenderOptions(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex,
        bool showStats, bool showQuestTracker, bool showDebugOption, bool debugEnabled, bool isOverlay)
    {
        int itemCount = showDebugOption ? 3 : 2;
        int boxW = 50;
        int boxH = itemCount + 8;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        if (isOverlay)
            AsciiDraw.FillOverlay(r, totalCols, totalRows);
        else
            r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "OPTIONS", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemY = sepY + 2;
        int tx = bx + 4;

        // Show Stats toggle
        {
            bool sel = selectedIndex == 0;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + "Show Stats: " + (showStats ? "ON" : "OFF");
            AsciiDraw.DrawString(r, tx, itemY, label, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
            itemY++;
        }

        // Show Quest Tracker toggle
        {
            bool sel = selectedIndex == 1;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + "Quest Tracker: " + (showQuestTracker ? "ON" : "OFF");
            AsciiDraw.DrawString(r, tx, itemY, label, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
            itemY++;
        }

        // Debug Mode toggle (only from main menu)
        if (showDebugOption)
        {
            bool sel = selectedIndex == 2;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + "Debug Mode: " + (debugEnabled ? "ON" : "OFF");
            AsciiDraw.DrawString(r, tx, itemY, label, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
            itemY++;
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter/\u2190\u2192 Toggle   Esc Back", RenderingTheme.Dim);
    }
}
