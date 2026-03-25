using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using SkiaSharp;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world as an ASCII tile grid using SkiaSharp.
/// Supports dynamic tile counts based on window size, game/HUD split, and menu screens.
/// Floats are used here for pixel coordinates and visual effects — this is the ONLY layer that uses them.
/// </summary>
public class TileRenderer : IDisposable
{
    public const int TileWidth = 12;
    public const int TileHeight = 16;
    public const int HudColumns = 14;

    private SKFont? _font;
    private SKPaint? _bgPaint;
    private SKPaint? _fgPaint;

    private static readonly char[] Cp437 = CreateCp437Map();

    // Colors used across menus and HUD
    private static readonly SKColor ColorBorder = new(180, 180, 180);
    private static readonly SKColor ColorTitle = new(255, 200, 50);
    private static readonly SKColor ColorNormal = new(180, 180, 180);
    private static readonly SKColor ColorSelected = new(255, 255, 255);
    private static readonly SKColor ColorDim = new(100, 100, 100);
    private static readonly SKColor ColorHpBar = new(220, 50, 50);
    private static readonly SKColor ColorHpFill = new(0, 200, 0);
    private static readonly SKColor ColorHpText = new(255, 80, 80);
    private static readonly SKColor ColorStats = new(200, 200, 200);
    private static readonly SKColor ColorLevel = new(255, 255, 100);
    private static readonly SKColor ColorItem = new(200, 180, 100);
    private static readonly SKColor ColorSkillReady = new(100, 255, 100);
    private static readonly SKColor ColorSkillCd = new(128, 128, 128);
    private static readonly SKColor ColorInv = new(150, 200, 255);
    private static readonly SKColor ColorOverlay = new(0, 0, 0, 160);

    private static readonly string[] MainMenuItems = ["Play Offline", "Play Online", "Help", "Quit"];
    private static readonly string[] PauseMenuItems = ["Resume", "Help", "Return to Main Menu"];

    private static readonly string[] HelpLines =
    [
        "CONTROLS",
        "",
        "W / \u2191    Move up",
        "S / \u2193    Move down",
        "A / \u2190    Move left",
        "D / \u2192    Move right",
        "Space    Wait a turn",
        "G        Pick up item",
        "1-4      Use item slot",
        "Q        Use skill 1",
        "E        Use skill 2",
        "X        Drop item",
        "Escape   Pause menu",
    ];

    public void Initialize()
    {
        var typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            ?? SKTypeface.FromFamilyName("Courier New", SKFontStyle.Normal)
            ?? SKTypeface.Default;

        _font = new SKFont(typeface, TileHeight - 2);
        _bgPaint = new SKPaint { Style = SKPaintStyle.Fill };
        _fgPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
    }

    // ── Game Screen ────────────────────────────────────────────

    public void RenderGame(SKCanvas canvas, ClientGameState state, int totalCols, int totalRows)
    {
        if (_font == null || _bgPaint == null || _fgPaint == null) return;

        canvas.Clear(SKColors.Black);

        int gameCols = totalCols - HudColumns;
        RenderGameWorld(canvas, state, gameCols, totalRows);
        RenderHudPanel(canvas, state, gameCols, totalRows);
    }

    private void RenderGameWorld(SKCanvas canvas, ClientGameState state, int gameCols, int totalRows)
    {
        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        int halfW = gameCols / 2;
        int halfH = totalRows / 2;

        for (int sx = 0; sx < gameCols; sx++)
        for (int sy = 0; sy < totalRows; sy++)
        {
            int worldX = cameraCenterX - halfW + sx;
            int worldY = cameraCenterY - halfH + sy;
            var tile = state.GetTile(worldX, worldY);

            float px = sx * TileWidth;
            float py = sy * TileHeight;

            var bgColor = IntToSkColor(tile.BgColor, tile.LightLevel);
            _bgPaint!.Color = bgColor;
            canvas.DrawRect(px, py, TileWidth, TileHeight, _bgPaint);

            if (tile.GlyphId > 0 && tile.LightLevel > 0)
            {
                _fgPaint!.Color = IntToSkColor(tile.FgColor, tile.LightLevel);
                char ch = tile.GlyphId < 256 ? Cp437[tile.GlyphId] : '?';
                canvas.DrawText(ch.ToString(), px + 1, py + TileHeight - 3, _font!, _fgPaint);
            }
        }

        // Entities
        foreach (var entity in state.Entities.Values)
        {
            int sx = entity.X - (cameraCenterX - halfW);
            int sy = entity.Y - (cameraCenterY - halfH);

            if (sx < 0 || sx >= gameCols || sy < 0 || sy >= totalRows) continue;

            float px = sx * TileWidth;
            float py = sy * TileHeight;

            _fgPaint!.Color = IntToSkColor(entity.FgColor, 10);
            char ch = entity.GlyphId < 256 ? Cp437[entity.GlyphId] : '?';
            canvas.DrawText(ch.ToString(), px + 1, py + TileHeight - 3, _font!, _fgPaint);

            if (entity.MaxHealth > 0 && entity.Health < entity.MaxHealth)
            {
                float ratio = (float)entity.Health / entity.MaxHealth;
                _fgPaint.Color = new SKColor(255, 0, 0, 180);
                canvas.DrawRect(px, py - 2, TileWidth, 2, _fgPaint);
                _fgPaint.Color = new SKColor(0, 255, 0, 180);
                canvas.DrawRect(px, py - 2, TileWidth * ratio, 2, _fgPaint);
            }
        }
    }

    private void RenderHudPanel(SKCanvas canvas, ClientGameState state, int hudStartCol, int totalRows)
    {
        // Dark background for HUD area
        float hx = hudStartCol * TileWidth;
        _bgPaint!.Color = new SKColor(15, 15, 20);
        canvas.DrawRect(hx, 0, HudColumns * TileWidth, totalRows * TileHeight, _bgPaint);

        // Vertical separator
        DrawChar(canvas, hudStartCol, 0, '┬', ColorBorder);
        for (int y = 1; y < totalRows - 1; y++)
            DrawChar(canvas, hudStartCol, y, '│', ColorBorder);
        DrawChar(canvas, hudStartCol, totalRows - 1, '┴', ColorBorder);

        int col = hudStartCol + 1;
        int innerW = HudColumns - 2;
        int row = 1;

        var hud = state.PlayerHud;
        if (hud == null)
        {
            DrawString(canvas, col, row, "No data", ColorDim);
            return;
        }

        // HP bar
        DrawString(canvas, col, row, "HP", ColorHpText);
        row++;
        int barW = innerW;
        float hpRatio = hud.MaxHealth > 0 ? (float)hud.Health / hud.MaxHealth : 0;
        int filled = (int)(barW * hpRatio);
        for (int i = 0; i < barW; i++)
            DrawChar(canvas, col + i, row, i < filled ? '█' : '░', i < filled ? ColorHpFill : ColorHpBar);
        row++;
        string hpText = $"{hud.Health}/{hud.MaxHealth}";
        DrawString(canvas, col, row, hpText, ColorHpText);
        row += 2;

        // Stats
        DrawString(canvas, col, row, $"ATK: {hud.Attack}", ColorStats);
        row++;
        DrawString(canvas, col, row, $"DEF: {hud.Defense}", ColorStats);
        row++;
        DrawString(canvas, col, row, $"Lv:  {hud.Level}", ColorLevel);
        row += 2;

        // Skills
        DrawString(canvas, col, row, "Skills", ColorTitle);
        row++;
        DrawHudSeparator(canvas, col, row, innerW);
        row++;
        for (int i = 0; i < Math.Min(hud.SkillIds.Length, 2); i++)
        {
            if (hud.SkillIds[i] == 0) continue;
            string key = i == 0 ? "Q" : "E";
            int cd = i < hud.SkillCooldowns.Length ? hud.SkillCooldowns[i] : 0;
            string text = cd > 0 ? $"[{key}] cd:{cd}" : $"[{key}] ready";
            DrawString(canvas, col, row, text, cd > 0 ? ColorSkillCd : ColorSkillReady);
            row++;
        }
        row++;

        // Inventory
        DrawString(canvas, col, row, "Items", ColorTitle);
        row++;
        DrawHudSeparator(canvas, col, row, innerW);
        row++;
        int itemsToShow = Math.Min(hud.InventoryNames.Length, 4);
        for (int i = 0; i < 4; i++)
        {
            if (i < itemsToShow && !string.IsNullOrEmpty(hud.InventoryNames[i]))
                DrawString(canvas, col, row, $"[{i + 1}]{hud.InventoryNames[i]}", ColorItem);
            else
                DrawString(canvas, col, row, $"[{i + 1}] ---", ColorDim);
            row++;
        }
        row++;

        DrawString(canvas, col, row, $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}", ColorInv);

        // Controls reminder at bottom
        int bottom = totalRows - 2;
        DrawString(canvas, col, bottom, "[Esc] Menu", ColorDim);
    }

    private void DrawHudSeparator(SKCanvas canvas, int col, int row, int width)
    {
        for (int i = 0; i < width; i++)
            DrawChar(canvas, col + i, row, '─', ColorDim);
    }

    // ── Main Menu ──────────────────────────────────────────────

    public void RenderMainMenu(SKCanvas canvas, int totalCols, int totalRows, int selectedIndex)
    {
        if (_font == null || _bgPaint == null || _fgPaint == null) return;

        canvas.Clear(SKColors.Black);

        int boxW = 40;
        int boxH = 18;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        DrawBox(canvas, bx, by, boxW, boxH, ColorBorder);

        // Title
        int titleY = by + 2;
        DrawCentered(canvas, totalCols, titleY, "R o g u e L i k e", ColorTitle);
        DrawCentered(canvas, totalCols, titleY + 1, ". N E T .", ColorTitle);

        // Decorative line
        int sepY = titleY + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            DrawChar(canvas, i, sepY, '─', ColorDim);

        // Menu items
        int itemStartY = sepY + 2;
        for (int i = 0; i < MainMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + MainMenuItems[i];
            int tx = bx + 6;
            DrawString(canvas, tx, itemStartY + i, text, sel ? ColorSelected : ColorNormal);
        }

        // Footer
        DrawCentered(canvas, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", ColorDim);
    }

    // ── Help Screen ────────────────────────────────────────────

    public void RenderHelp(SKCanvas canvas, int totalCols, int totalRows, bool isOverlay = false)
    {
        if (_font == null || _bgPaint == null || _fgPaint == null) return;

        int boxW = 36;
        int boxH = HelpLines.Length + 6;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        if (isOverlay)
            FillOverlay(canvas, totalCols, totalRows);
        else
            canvas.Clear(SKColors.Black);

        DrawBox(canvas, bx, by, boxW, boxH, ColorBorder, new SKColor(10, 10, 15));

        int row = by + 2;
        for (int i = 0; i < HelpLines.Length; i++)
        {
            var color = i == 0 ? ColorTitle : ColorNormal;
            if (string.IsNullOrEmpty(HelpLines[i]))
            {
                row++;
                continue;
            }
            DrawString(canvas, bx + 3, row, HelpLines[i], color);
            row++;
        }

        DrawCentered(canvas, totalCols, by + boxH - 2, "Press Esc to go back", ColorDim);
    }

    // ── Pause Menu ─────────────────────────────────────────────

    public void RenderPauseOverlay(SKCanvas canvas, int totalCols, int totalRows, int selectedIndex)
    {
        if (_font == null || _bgPaint == null || _fgPaint == null) return;

        FillOverlay(canvas, totalCols, totalRows);

        int boxW = 30;
        int boxH = PauseMenuItems.Length + 8;
        int bx = (totalCols - boxW) / 2;
        int by = (totalRows - boxH) / 2;

        DrawBox(canvas, bx, by, boxW, boxH, ColorBorder, new SKColor(10, 10, 15));

        DrawCentered(canvas, totalCols, by + 2, "PAUSED", ColorTitle);

        int sepY = by + 3;
        for (int i = bx + 2; i < bx + boxW - 2; i++)
            DrawChar(canvas, i, sepY, '─', ColorDim);

        int itemY = sepY + 2;
        for (int i = 0; i < PauseMenuItems.Length; i++)
        {
            bool sel = i == selectedIndex;
            string prefix = sel ? " \u25ba " : "   ";
            string text = prefix + PauseMenuItems[i];
            int tx = bx + 4;
            DrawString(canvas, tx, itemY + i, text, sel ? ColorSelected : ColorNormal);
        }

        DrawCentered(canvas, totalCols, by + boxH - 2, "\u2191\u2193 Navigate   Enter Select", ColorDim);
    }

    // ── Drawing Helpers ────────────────────────────────────────

    private void DrawChar(SKCanvas canvas, int tileX, int tileY, char ch, SKColor color)
    {
        float px = tileX * TileWidth;
        float py = tileY * TileHeight;
        _fgPaint!.Color = color;
        canvas.DrawText(ch.ToString(), px + 1, py + TileHeight - 3, _font!, _fgPaint);
    }

    private void DrawString(SKCanvas canvas, int tileX, int tileY, string text, SKColor color)
    {
        _fgPaint!.Color = color;
        for (int i = 0; i < text.Length; i++)
        {
            float px = (tileX + i) * TileWidth;
            float py = tileY * TileHeight;
            canvas.DrawText(text[i].ToString(), px + 1, py + TileHeight - 3, _font!, _fgPaint);
        }
    }

    private void DrawCentered(SKCanvas canvas, int totalCols, int tileY, string text, SKColor color)
    {
        int tx = (totalCols - text.Length) / 2;
        DrawString(canvas, tx, tileY, text, color);
    }

    private void DrawBox(SKCanvas canvas, int x, int y, int w, int h, SKColor borderColor, SKColor? fillColor = null)
    {
        if (fillColor.HasValue)
        {
            _bgPaint!.Color = fillColor.Value;
            for (int bx = x + 1; bx < x + w - 1; bx++)
            for (int by = y + 1; by < y + h - 1; by++)
                canvas.DrawRect(bx * TileWidth, by * TileHeight, TileWidth, TileHeight, _bgPaint);
        }

        DrawChar(canvas, x, y, '┌', borderColor);
        DrawChar(canvas, x + w - 1, y, '┐', borderColor);
        DrawChar(canvas, x, y + h - 1, '└', borderColor);
        DrawChar(canvas, x + w - 1, y + h - 1, '┘', borderColor);

        for (int i = 1; i < w - 1; i++)
        {
            DrawChar(canvas, x + i, y, '─', borderColor);
            DrawChar(canvas, x + i, y + h - 1, '─', borderColor);
        }
        for (int i = 1; i < h - 1; i++)
        {
            DrawChar(canvas, x, y + i, '│', borderColor);
            DrawChar(canvas, x + w - 1, y + i, '│', borderColor);
        }
    }

    private void FillOverlay(SKCanvas canvas, int totalCols, int totalRows)
    {
        _bgPaint!.Color = ColorOverlay;
        canvas.DrawRect(0, 0, totalCols * TileWidth, totalRows * TileHeight, _bgPaint);
    }

    // ── Utilities ──────────────────────────────────────────────

    private static SKColor IntToSkColor(int packedRgb, int lightLevel)
    {
        if (lightLevel <= 0) return SKColors.Black;

        float brightness = lightLevel / 10f;
        byte r = (byte)((packedRgb >> 16 & 0xFF) * brightness);
        byte g = (byte)((packedRgb >> 8 & 0xFF) * brightness);
        byte b = (byte)((packedRgb & 0xFF) * brightness);
        return new SKColor(r, g, b);
    }

    private static char[] CreateCp437Map()
    {
        var map = new char[256];
        string cp437 = "\0☺☻♥♦♣♠•◘○◙♂♀♪♫☼►◄↕‼¶§▬↨↑↓→←∟↔▲▼" +
                        " !\"#$%&'()*+,-./0123456789:;<=>?" +
                        "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_" +
                        "`abcdefghijklmnopqrstuvwxyz{|}~⌂" +
                        "ÇüéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜ¢£¥₧ƒ" +
                        "áíóúñÑªº¿⌐¬½¼¡«»░▒▓│┤╡╢╖╕╣║╗╝╜╛┐" +
                        "└┴┬├─┼╞╟╚╔╩╦╠═╬╧╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀" +
                        "αßΓπΣσµτΦΘΩδ∞φε∩≡±≥≤⌠⌡÷≈°∙·√ⁿ²■\u00A0";

        for (int i = 0; i < Math.Min(cp437.Length, 256); i++)
            map[i] = cp437[i];

        return map;
    }

    public void Dispose()
    {
        _font?.Dispose();
        _bgPaint?.Dispose();
        _fgPaint?.Dispose();
        GC.SuppressFinalize(this);
    }
}
