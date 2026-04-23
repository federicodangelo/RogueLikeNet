using Engine.Core;
using Engine.Platform;

namespace RogueLikeNet.Client.Core.Rendering.Menus;

public sealed class ConnectingRenderer
{
    public void RenderConnecting(ISpriteRenderer r, int totalCols, int totalRows, string? errorMessage = null)
    {
        r.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);

        int boxW = 50;
        int boxH = errorMessage != null ? 10 : 7;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        AsciiDraw.DrawBox(r, bx, by, boxW, boxH, RenderingTheme.Border, new Color4(10, 10, 15, 255));

        if (errorMessage != null)
        {
            AsciiDraw.DrawCentered(r, totalCols, by + 2, "CONNECTION FAILED", RenderingTheme.HpText);
            string msg = errorMessage.Length > boxW - 4 ? errorMessage[..(boxW - 4)] : errorMessage;
            AsciiDraw.DrawCentered(r, totalCols, by + 4, msg, RenderingTheme.Normal);
            AsciiDraw.DrawCentered(r, totalCols, by + boxH - 2, "Press Enter to return", RenderingTheme.Dim);
        }
        else
        {
            AsciiDraw.DrawCentered(r, totalCols, by + 3, "Connecting...", RenderingTheme.Normal);
        }
    }
}
