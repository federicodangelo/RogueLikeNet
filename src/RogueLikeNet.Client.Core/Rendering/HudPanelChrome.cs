using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Shared drawing primitives for the vertical HUD panel used by all playing-overlay
/// screens (Inventory, Crafting, Quest Log, NPC Dialogue). Keeps the panel chrome
/// visually consistent and avoids duplicating the border/header code.
/// </summary>
public static class HudPanelChrome
{
    /// <summary>
    /// Draws the HUD panel background plus the left-hand vertical border (┬ │ ┴).
    /// </summary>
    public static void DrawBorder(ISpriteRenderer r, int hudStartCol, int totalRows)
    {
        float hx = hudStartCol * AsciiDraw.TileWidth;
        r.DrawRectScreen(hx, 0, AsciiDraw.HudColumns * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.HudBg);

        AsciiDraw.DrawChar(r, hudStartCol, 0, '\u252C', RenderingTheme.Border);
        for (int y = 1; y < totalRows - 1; y++)
            AsciiDraw.DrawChar(r, hudStartCol, y, '\u2502', RenderingTheme.Border);
        AsciiDraw.DrawChar(r, hudStartCol, totalRows - 1, '\u2534', RenderingTheme.Border);
    }

    /// <summary>
    /// Draws a panel header row: <paramref name="title"/> in title color at the left,
    /// <c>[ESC]</c> hint in dim color at the right. Title is truncated to fit <paramref name="innerW"/>.
    /// </summary>
    public static void DrawHeader(ISpriteRenderer r, int innerCol, int row, int innerW, string title)
    {
        if (title.Length > innerW) title = title[..innerW];
        AsciiDraw.DrawString(r, innerCol, row, title, RenderingTheme.Title);
        AsciiDraw.DrawString(r, innerCol + innerW - 5, row, "[ESC]", RenderingTheme.Dim);
    }
}
