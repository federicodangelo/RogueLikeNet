using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world tile grid: background tiles with FOW, glow effects, and entity sprites.
/// </summary>
public sealed class GameWorldRenderer
{
    public void Render(ISpriteRenderer r, ClientGameState state, int gameCols, int totalRows,
        float shakeX, float shakeY, int tileW = 0, int tileH = 0, float fontScale = 0f, bool debugLightOff = false)
    {
        using var _ = new TimeMeasurer("GameWorldRenderer.Render");

        if (tileW <= 0) tileW = AsciiDraw.TileWidth;
        if (tileH <= 0) tileH = AsciiDraw.TileHeight;
        if (fontScale <= 0f) fontScale = AsciiDraw.FontScale;

        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        int halfW = gameCols / 2;
        int halfH = totalRows / 2;

        float GetTileBrightness(int worldX, int worldY, int lightLevel)
        {
            var visible = state.IsVisible(worldX, worldY);
            var explored = state.IsExplored(worldX, worldY);
            if (!visible && !explored)
                return 0f;
            var minBrightness = explored ? AsciiDraw.FogBrightness : 0f;
            var brightness = debugLightOff ? 1f : visible ? Math.Max(AsciiDraw.LightLevelToBrightness(lightLevel), minBrightness) : AsciiDraw.FogBrightness;
            return brightness;
        }

        // Pass 1: tile backgrounds and foreground glyphs (batched)
        using (new TimeMeasurer("Pass 1: Tiles"))
        {
            r.DrawGlyphGridScreen(shakeX, shakeY, gameCols, totalRows, tileW, tileH, fontScale,
                (col, row) =>
                {
                    var worldX = cameraCenterX - halfW + col;
                    var worldY = cameraCenterY - halfH + row;
                    var (tile, lightLevel) = state.GetTileAndLightLevel(worldX, worldY);

                    var emptyTile = tile.GlyphId == 0;

                    var brightness = GetTileBrightness(worldX, worldY, lightLevel);

                    if (emptyTile || brightness <= 0f)
                    {
                        return new GlyphTile('\0', default, RenderingTheme.Black);
                    }

                    var bgColor = AsciiDraw.ApplyBrightness(AsciiDraw.IntToColor4(tile.BgColor), brightness);
                    var fgColor = AsciiDraw.ApplyBrightness(AsciiDraw.IntToColor4(tile.FgColor), brightness);
                    var ch = AsciiDraw.GlyphIdToChar(tile.GlyphId);
                    return new GlyphTile(ch, fgColor, bgColor);
                });
        }

        // Pass 2: glow effects behind torches and light-emitting tiles (visible only)
        using (new TimeMeasurer("Pass 2: Glow Effects"))
        {
            for (int sx = 0; sx < gameCols; sx++)
                for (int sy = 0; sy < totalRows; sy++)
                {
                    int worldX = cameraCenterX - halfW + sx;
                    int worldY = cameraCenterY - halfH + sy;

                    if (!state.IsVisible(worldX, worldY)) continue;

                    var (tile, lightLevel) = state.GetTileAndLightLevel(worldX, worldY);

                    if (lightLevel < 5) continue;

                    if (tile.GlyphId == TileDefinitions.GlyphTorch)
                    {
                        float cx = sx * tileW + tileW * 0.5f + shakeX;
                        float cy = sy * tileH + tileH * 0.5f + shakeY;
                        float radius = tileW * 2.5f;
                        var inner = new Color4(255, 200, 100, 40);
                        var outer = new Color4(255, 150, 50, 0);
                        r.DrawFilledCircleScreen(cx, cy, radius, inner, outer, radius * 0.3f, 16);
                    }
                    else if (tile.Type == TileType.Lava)
                    {
                        float cx = sx * tileW + tileW * 0.5f + shakeX;
                        float cy = sy * tileH + tileH * 0.5f + shakeY;
                        float radius = tileW * 1.5f;
                        var inner = new Color4(255, 80, 20, 25);
                        var outer = new Color4(255, 40, 0, 0);
                        r.DrawFilledCircleScreen(cx, cy, radius, inner, outer, radius * 0.3f, 12);
                    }
                }
        }

        // Pass 3: entities
        using (new TimeMeasurer("Pass 3: Entities"))
        {
            foreach (var entity in state.Entities.Values)
            {
                var sx = entity.X - (cameraCenterX - halfW);
                var sy = entity.Y - (cameraCenterY - halfH);

                if (sx < 0 || sx >= gameCols || sy < 0 || sy >= totalRows) continue;

                var px = sx * tileW + shakeX;
                var py = sy * tileH + shakeY;

                var brightness = GetTileBrightness(entity.X, entity.Y, state.GetLightLevel(entity.X, entity.Y));

                if (brightness <= 0f)
                    continue;

                var fgColor = AsciiDraw.ApplyBrightness(AsciiDraw.IntToColor4(entity.FgColor), brightness);
                var ch = AsciiDraw.GlyphIdToChar(entity.GlyphId);
                r.DrawTextScreen(px, py, ch.ToString(), fgColor, fontScale);

                if (entity.MaxHealth > 0 && entity.Health < entity.MaxHealth)
                {
                    var ratio = (float)entity.Health / entity.MaxHealth;
                    r.DrawRectScreen(px, py - 2, tileW, 2, RenderingTheme.HpBar.WithAlpha(180));
                    r.DrawRectScreen(px, py - 2, tileW * ratio, 2, RenderingTheme.HpFill.WithAlpha(180));
                }
            }
        }
    }

    public void RenderMinimap(ISpriteRenderer r, ClientGameState state, int gameCols, int totalRows)
    {
        int mapSize = 40;
        int pixelSize = 3;
        int minimapPx = mapSize * pixelSize;

        float baseX = gameCols * AsciiDraw.TileWidth - minimapPx - 4;
        float baseY = 4;

        r.DrawRectScreen(baseX - 1, baseY - 1, minimapPx + 2, minimapPx + 2, new Color4(40, 40, 50, 200));

        int cx = state.PlayerX;
        int cy = state.PlayerY;
        int half = mapSize / 2;

        for (int dx = 0; dx < mapSize; dx++)
            for (int dy = 0; dy < mapSize; dy++)
            {
                int wx = cx - half + dx;
                int wy = cy - half + dy;

                if (!state.IsExplored(wx, wy))
                    continue;

                var tile = state.GetTile(wx, wy);
                if (tile.GlyphId == 0)
                    continue;

                bool visible = state.IsVisible(wx, wy);

                Color4 dotColor;
                if (tile.Type == TileType.Wall)
                    dotColor = visible ? new Color4(120, 120, 140, 255) : new Color4(50, 50, 60, 255);
                else if (tile.Type == TileType.Lava)
                    dotColor = visible ? new Color4(255, 80, 20, 255) : new Color4(80, 30, 10, 255);
                else if (tile.Type == TileType.Water)
                    dotColor = visible ? new Color4(70, 130, 255, 255) : new Color4(25, 45, 80, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphTorch)
                    dotColor = visible ? new Color4(255, 200, 100, 255) : new Color4(80, 65, 35, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphDoor)
                    dotColor = visible ? new Color4(180, 130, 60, 255) : new Color4(60, 45, 25, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphStairsDown || tile.GlyphId == TileDefinitions.GlyphStairsUp)
                    dotColor = visible ? new Color4(255, 255, 80, 255) : new Color4(80, 80, 30, 255);
                else if (tile.Type == TileType.Decoration)
                    dotColor = visible ? new Color4(80, 80, 60, 255) : new Color4(30, 30, 25, 255);
                else if (tile.Type == TileType.Floor)
                    dotColor = visible ? new Color4(60, 60, 70, 255) : new Color4(25, 25, 30, 255);
                else
                    dotColor = visible ? new Color4(50, 50, 60, 255) : new Color4(20, 20, 25, 255);

                r.DrawRectScreen(baseX + dx * pixelSize, baseY + dy * pixelSize, pixelSize, pixelSize, dotColor);
            }

        foreach (var entity in state.Entities.Values)
        {
            int dx = entity.X - (cx - half);
            int dy = entity.Y - (cy - half);
            if (dx < 0 || dx >= mapSize || dy < 0 || dy >= mapSize) continue;

            Color4 entityColor = entity.GlyphId == TileDefinitions.GlyphPlayer
                ? new Color4(100, 255, 100, 255)
                : new Color4(255, 80, 80, 255);
            r.DrawRectScreen(baseX + dx * pixelSize, baseY + dy * pixelSize, pixelSize, pixelSize, entityColor);
        }

        r.DrawRectScreen(baseX + half * pixelSize, baseY + half * pixelSize,
            pixelSize, pixelSize, new Color4(255, 255, 255, 255));
    }
}
