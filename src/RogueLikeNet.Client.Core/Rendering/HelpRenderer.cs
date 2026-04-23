using Engine.Core;
using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering;

public sealed class HelpRenderer
{
    private static readonly string[] HelpLines =
    [
        "CONTROLS",
        "",
        "W / \u2191    Move / Attack up",
        "S / \u2193    Move / Attack down",
        "A / \u2190    Move / Attack left",
        "D / \u2192    Move / Attack right",
        "1-8      Use quick slot",
        "G        Pick up item",
        "X        Drop item",
        "P        Pickup placeable",
        "F        Melee / Ranged attack",
        "M        Cast spell",
        "L        Look around",
        "E        Interact",
        "I        Inventory",
        "C        Crafting",
        "Escape   Ingame menu",
    ];

    public void RenderHelp(ISpriteRenderer r, int totalCols, int totalRows, bool isOverlay = false)
    {
        int boxW = 36;
        int boxH = HelpLines.Length + 6;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        if (isOverlay)
            AsciiDraw.FillOverlay(r, totalCols, totalRows);
        else
            r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        int row = by + 2;
        for (int i = 0; i < HelpLines.Length; i++)
        {
            var color = i == 0 ? RenderingTheme.Title : RenderingTheme.Normal;
            if (string.IsNullOrEmpty(HelpLines[i]))
            {
                row++;
                continue;
            }
            AsciiDraw.DrawString(r, bx + 3, row, HelpLines[i], color);
            row++;
        }

        AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "Press Esc to go back", RenderingTheme.Dim);
    }
}
