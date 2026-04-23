using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering.Menus;

public sealed class MainMenuRenderer
{
    private static readonly string[] MainMenuItems = ["New Game/Load Game", "Play Online", "Admin Online", "Options", "Help", "Quit"];
    public const int MainMenuPlayOfflineIndex = 0;
    public const int MainMenuPlayOnlineIndex = 1;
    public const int MainMenuAdminOnlineIndex = 2;
    public const int MainMenuOptionsIndex = 3;
    public const int MainMenuHelpIndex = 4;
    public const int MainMenuQuitIndex = 5;

    public void RenderMainMenu(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 46;
        int boxH = 20;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border);

        int titleY = by + 2;
        AsciiDraw.DrawCentered(r, totalCols, titleY, "R o g u e L i k e", RenderingTheme.Title);
        AsciiDraw.DrawCentered(r, totalCols, titleY + 1, ". N E T .", RenderingTheme.Title);

        int sepY = titleY + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemStartY = sepY + 2;
        for (int i = 0; i < MainMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string label = prefix + MainMenuItems[i];
            int tx = bx + 6;
            AsciiDraw.DrawString(r, tx, itemStartY + i, label, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        string footer = "\u2191\u2193 Navigate   Enter Select";
        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, footer, RenderingTheme.Dim);
    }
}
