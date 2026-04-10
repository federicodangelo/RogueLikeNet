using System.Runtime.CompilerServices;
using Engine.Core;
using Engine.Platform;
using Engine.Rendering.Base;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Low-level ASCII drawing primitives used by all renderers.
/// </summary>
public static class AsciiDraw
{
    public const int TileWidth = (int)(MiniBitmapFont.GlyphWidth * FontScale);
    public const int TileHeight = (int)(MiniBitmapFont.GlyphHeight * FontScale);
    public const int HudColumns = 30;
    public const float FontScale = 1.5f;

    public const float FogBrightness = 0.12f;

    private static readonly char[] Cp437 = MiniBitmapFont.Cp437ToUnicode;

    public static char GlyphIdToChar(int glyphId) => glyphId >= 0 && glyphId < 256 ? Cp437[glyphId] : '?';

    public static void DrawChar(ISpriteRenderer r, int tileX, int tileY, char ch, Color4 color)
    {
        float px = tileX * TileWidth;
        float py = tileY * TileHeight;
        r.DrawTextScreen(px, py, ch.ToString(), color, FontScale);
    }

    public static void DrawString(ISpriteRenderer r, int tileX, int tileY, string text, Color4 color)
    {
        float px = tileX * TileWidth;
        float py = tileY * TileHeight;
        r.DrawTextScreen(px, py, text, color, FontScale);
    }

    public static void DrawCentered(ISpriteRenderer r, int totalCols, int tileY, string text, Color4 color)
    {
        int tx = (totalCols - text.Length) / 2;
        DrawString(r, tx, tileY, text, color);
    }

    public static void DrawBox(ISpriteRenderer r, int x, int y, int w, int h, Color4 borderColor, Color4? fillColor = null)
    {
        if (fillColor.HasValue)
        {
            float fx = x * TileWidth;
            float fy = y * TileHeight;
            float fw = w * TileWidth;
            float fh = h * TileHeight;
            r.DrawRectScreen(fx, fy, fw, fh, fillColor.Value);
        }

        for (int i = 1; i < w - 1; i++)
        {
            DrawChar(r, x + i, y, '\u2500', borderColor);
            DrawChar(r, x + i, y + h - 1, '\u2500', borderColor);
        }
        for (int i = 1; i < h - 1; i++)
        {
            DrawChar(r, x, y + i, '\u2502', borderColor);
            DrawChar(r, x + w - 1, y + i, '\u2502', borderColor);
        }

        DrawChar(r, x, y, '\u250C', borderColor);
        DrawChar(r, x + w - 1, y, '\u2510', borderColor);
        DrawChar(r, x, y + h - 1, '\u2514', borderColor);
        DrawChar(r, x + w - 1, y + h - 1, '\u2518', borderColor);
    }

    public static void FillOverlay(ISpriteRenderer r, int totalCols, int totalRows)
    {
        r.DrawRectScreen(0, 0, totalCols * TileWidth, totalRows * TileHeight, RenderingTheme.Overlay);
    }

    public static void DrawHudSeparator(ISpriteRenderer r, int col, int row, int width)
    {
        for (int i = 0; i < width; i++)
            DrawChar(r, col + i, row, '\u2500', RenderingTheme.Dim);
    }

    public static void DrawStatLine(ISpriteRenderer r, int x, int y, string label, int value, int width)
    {
        DrawString(r, x, y, $"{label}:", RenderingTheme.StatLabel);
        string valStr = value > 0 ? $"+{value}" : value.ToString();
        var valColor = value > 0 ? RenderingTheme.StatPositive : value < 0 ? RenderingTheme.StatNegative : RenderingTheme.StatZero;
        int valX = x + width - valStr.Length;
        DrawString(r, valX, y, valStr, valColor);
    }

    public static float LightLevelToBrightness(int lightLevel)
    {
        if (lightLevel <= 0) return 0;
        var raw = Math.Clamp(lightLevel / 10f, 0f, 1f);
        var brightness = 0.12f + 0.88f * MathF.Pow(raw, 0.65f);
        return brightness;
    }

    public static string CategoryTag(int category) => ItemDefinition.CategoryTag((ItemCategory)category);

    public static string ItemDisplayName(int itemTypeId)
    {
        var def = GameData.Instance.Items.Get(itemTypeId);
        string name = def?.Name ?? "Unknown";
        return name;
    }
}
