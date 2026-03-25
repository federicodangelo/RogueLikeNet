using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using SkiaSharp;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world as an ASCII tile grid using SkiaSharp.
/// Floats are used here for pixel coordinates and visual effects — this is the ONLY layer that uses them.
/// </summary>
public class TileRenderer : IDisposable
{
    private const int TileWidth = 12;
    private const int TileHeight = 16;
    private const int ViewTilesX = 80;
    private const int ViewTilesY = 50;

    private SKFont? _font;
    private SKPaint? _bgPaint;
    private SKPaint? _fgPaint;

    // CP437 character map (first 256 characters)
    private static readonly char[] Cp437 = CreateCp437Map();

    public int PixelWidth => ViewTilesX * TileWidth;
    public int PixelHeight => ViewTilesY * TileHeight;

    public void Initialize()
    {
        var typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
            ?? SKTypeface.FromFamilyName("Courier New", SKFontStyle.Normal)
            ?? SKTypeface.Default;

        _font = new SKFont(typeface, TileHeight - 2);
        _bgPaint = new SKPaint { Style = SKPaintStyle.Fill };
        _fgPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
    }

    public void Render(SKCanvas canvas, ClientGameState state)
    {
        if (_font == null || _bgPaint == null || _fgPaint == null) return;

        canvas.Clear(SKColors.Black);

        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        int halfW = ViewTilesX / 2;
        int halfH = ViewTilesY / 2;

        // Render tiles
        for (int screenX = 0; screenX < ViewTilesX; screenX++)
        for (int screenY = 0; screenY < ViewTilesY; screenY++)
        {
            int worldX = cameraCenterX - halfW + screenX;
            int worldY = cameraCenterY - halfH + screenY;
            var tile = state.GetTile(worldX, worldY);

            float px = screenX * TileWidth;
            float py = screenY * TileHeight;

            // Background
            var bgColor = IntToSkColor(tile.BgColor, tile.LightLevel);
            _bgPaint.Color = bgColor;
            canvas.DrawRect(px, py, TileWidth, TileHeight, _bgPaint);

            // Glyph
            if (tile.GlyphId > 0 && tile.LightLevel > 0)
            {
                var fgColor = IntToSkColor(tile.FgColor, tile.LightLevel);
                _fgPaint.Color = fgColor;

                char ch = tile.GlyphId < 256 ? Cp437[tile.GlyphId] : '?';
                string text = ch.ToString();
                canvas.DrawText(text, px + 1, py + TileHeight - 3, _font, _fgPaint);
            }
        }

        // Render entities
        foreach (var entity in state.Entities.Values)
        {
            int screenX = entity.X - (cameraCenterX - halfW);
            int screenY = entity.Y - (cameraCenterY - halfH);

            if (screenX < 0 || screenX >= ViewTilesX || screenY < 0 || screenY >= ViewTilesY)
                continue;

            float px = screenX * TileWidth;
            float py = screenY * TileHeight;

            // Draw entity glyph
            var entityColor = IntToSkColor(entity.FgColor, 10); // entities always fully lit
            _fgPaint.Color = entityColor;

            char ch = entity.GlyphId < 256 ? Cp437[entity.GlyphId] : '?';
            canvas.DrawText(ch.ToString(), px + 1, py + TileHeight - 3, _font, _fgPaint);

            // Health bar for damaged entities
            if (entity.MaxHealth > 0 && entity.Health < entity.MaxHealth)
            {
                float healthRatio = (float)entity.Health / entity.MaxHealth;
                float barWidth = TileWidth * healthRatio;
                _fgPaint.Color = new SKColor(255, 0, 0, 180);
                canvas.DrawRect(px, py - 2, TileWidth, 2, _fgPaint);
                _fgPaint.Color = new SKColor(0, 255, 0, 180);
                canvas.DrawRect(px, py - 2, barWidth, 2, _fgPaint);
            }
        }

        // Render HUD
        RenderHud(canvas, state);
    }

    private void RenderHud(SKCanvas canvas, ClientGameState state)
    {
        if (_font == null || _fgPaint == null || _bgPaint == null) return;

        var hud = state.PlayerHud;
        if (hud == null) return;

        // HUD background bar at bottom
        float hudY = (ViewTilesY - 2) * TileHeight;
        _bgPaint.Color = new SKColor(0, 0, 0, 200);
        canvas.DrawRect(0, hudY, ViewTilesX * TileWidth, TileHeight * 2, _bgPaint);

        float x = 4;
        float y = hudY + TileHeight - 2;

        // HP
        _fgPaint.Color = new SKColor(255, 80, 80);
        string hpText = $"HP:{hud.Health}/{hud.MaxHealth}";
        canvas.DrawText(hpText, x, y, _font, _fgPaint);
        x += hpText.Length * 8 + 12;

        // ATK/DEF
        _fgPaint.Color = new SKColor(200, 200, 200);
        string statsText = $"ATK:{hud.Attack} DEF:{hud.Defense}";
        canvas.DrawText(statsText, x, y, _font, _fgPaint);
        x += statsText.Length * 8 + 12;

        // Level
        _fgPaint.Color = new SKColor(255, 255, 100);
        string lvlText = $"Lv:{hud.Level}";
        canvas.DrawText(lvlText, x, y, _font, _fgPaint);
        x += lvlText.Length * 8 + 12;

        // Inventory count
        _fgPaint.Color = new SKColor(150, 200, 255);
        string invText = $"Inv:{hud.InventoryCount}/{hud.InventoryCapacity}";
        canvas.DrawText(invText, x, y, _font, _fgPaint);

        // Second line: skills and inventory items
        float x2 = 4;
        float y2 = hudY + TileHeight * 2 - 2;

        // Skills
        if (hud.SkillIds.Length > 0)
        {
            for (int i = 0; i < Math.Min(hud.SkillIds.Length, 2); i++)
            {
                if (hud.SkillIds[i] == 0) continue;
                string key = i == 0 ? "Q" : "E";
                int cd = i < hud.SkillCooldowns.Length ? hud.SkillCooldowns[i] : 0;
                _fgPaint.Color = cd > 0 ? new SKColor(128, 128, 128) : new SKColor(100, 255, 100);
                string skillText = cd > 0 ? $"[{key}]Skill{i}({cd})" : $"[{key}]Skill{i}";
                canvas.DrawText(skillText, x2, y2, _font, _fgPaint);
                x2 += skillText.Length * 8 + 8;
            }
        }

        // Inventory items (first 4)
        if (hud.InventoryNames.Length > 0)
        {
            for (int i = 0; i < Math.Min(hud.InventoryNames.Length, 4); i++)
            {
                _fgPaint.Color = new SKColor(200, 180, 100);
                string itemText = $"[{i + 1}]{hud.InventoryNames[i]}";
                canvas.DrawText(itemText, x2, y2, _font, _fgPaint);
                x2 += itemText.Length * 8 + 8;
            }
        }

        // HP bar visual
        float barX = 4;
        float barY = hudY - 4;
        float barWidth = 200;
        float barHeight = 3;
        _bgPaint.Color = new SKColor(100, 0, 0);
        canvas.DrawRect(barX, barY, barWidth, barHeight, _bgPaint);
        if (hud.MaxHealth > 0)
        {
            float hpRatio = (float)hud.Health / hud.MaxHealth;
            _bgPaint.Color = new SKColor(0, 200, 0);
            canvas.DrawRect(barX, barY, barWidth * hpRatio, barHeight, _bgPaint);
        }
    }

    /// <summary>
    /// Converts packed int color (0xRRGGBB) + light level (0-10) to SKColor.
    /// This is the visualization boundary: int game data → float visual brightness.
    /// </summary>
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
        // Standard Code Page 437 character mapping
        var map = new char[256];
        // Control characters mapped to special symbols
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
