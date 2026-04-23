using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;

namespace RogueLikeNet.Client.Core.Rendering;

public sealed class PausedRenderer
{
    private static readonly string[] PauseMenuItems = ["Resume", "Options", "Help", "Return to Main Menu"];
    public const int PauseMenuResumeIndex = 0;
    public const int PauseMenuOptionsIndex = 1;
    public const int PauseMenuHelpIndex = 2;
    public const int PauseMenuMainMenuIndex = 3;

    public void RenderPauseOverlay(ISpriteRenderer r, int totalCols, int totalRows, int selectedIndex, DebugSettings? debug = null)
    {
        AsciiDraw.FillOverlay(r, totalCols, totalRows);

        bool showDebug = debug is { Enabled: true };
        int debugLines = showDebug ? 11 : 0;
        int boxW = 38;
        int boxH = PauseMenuItems.Length + 6 + debugLines;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        AsciiDraw.DrawCentered(r, totalCols, by + 1, "PAUSED", RenderingTheme.Title);

        int sepY = by + 2;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            AsciiDraw.DrawChar(r, i, sepY, '\u2500', RenderingTheme.Dim);

        int itemY = sepY + 1;
        for (int i = 0; i < PauseMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + PauseMenuItems[i];
            int tx = bx + 4;
            AsciiDraw.DrawString(r, tx, itemY + i, text, sel ? RenderingTheme.Selected : RenderingTheme.Normal);
        }

        if (showDebug)
        {
            int debugY = itemY + PauseMenuItems.Length + 1;
            for (int i = bx + 2; i < bx + boxW - 2; i++)
                AsciiDraw.DrawChar(r, i, debugY, '\u2500', RenderingTheme.Dim);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, "DEBUG MODE", RenderingTheme.Title);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"V  Visibility: {(debug!.VisibilityOff ? "OFF" : "ON")}", debug.VisibilityOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"C  Collisions: {(debug.CollisionsOff ? "OFF" : "ON")}", debug.CollisionsOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"H  Invulnerable: {(debug.Invulnerable ? "ON" : "OFF")}", debug.Invulnerable ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"L  Lighting: {(debug.LightOff ? "OFF" : "ON")}", debug.LightOff ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"M  Max Speed: {(debug.MaxSpeed ? "ON" : "OFF")}", debug.MaxSpeed ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"F  Free Craft: {(debug.FreeCrafting ? "ON" : "OFF")}", debug.FreeCrafting ? RenderingTheme.Selected : RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"Z  Toggle All", RenderingTheme.Normal);
            debugY++;
            AsciiDraw.DrawString(r, bx + 4, debugY, $"+/-/0  Zoom: {debug.ZoomLevel}", RenderingTheme.Normal);
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", RenderingTheme.Dim);
    }
}
