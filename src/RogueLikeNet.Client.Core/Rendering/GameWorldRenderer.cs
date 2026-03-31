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
    private struct PrecalculatedTile
    {
        public TileInfo tile;
        public float brightness;
        public int lightLevel;
        public bool visible;
        public bool explored;
    }

    private PrecalculatedTile[] _precalculatedBuffer = Array.Empty<PrecalculatedTile>();

    private PrecalculatedTile[] PrecalculateTiles(ClientGameState state, int cameraCenterX, int cameraCenterY, int visibleCols, int visibleRows, bool debugLightOff)
    {
        int requiredSize = visibleCols * visibleRows;
        if (_precalculatedBuffer.Length < requiredSize)
        {
            _precalculatedBuffer = new PrecalculatedTile[requiredSize];
        }

        var precalculated = _precalculatedBuffer;
        for (int row = 0; row < visibleRows; row++)
        {
            var worldY = cameraCenterY - visibleRows / 2 + row;
            for (int col = 0; col < visibleCols; col++)
            {
                var worldX = cameraCenterX - visibleCols / 2 + col;
                var (tile, lightLevel) = state.GetTileAndLightLevel(worldX, worldY);

                var visible = state.IsVisible(worldX, worldY);
                var explored = state.IsExplored(worldX, worldY);
                var minBrightness = explored ? AsciiDraw.FogBrightness : 0f;
                var brightness =
                    !visible && !explored ? 0f :
                        debugLightOff ? 1f :
                        visible ?
                            Math.Max(AsciiDraw.LightLevelToBrightness(lightLevel), minBrightness) :
                            minBrightness;

                precalculated[row * visibleCols + col] = new PrecalculatedTile
                {
                    tile = tile,
                    brightness = brightness,
                    lightLevel = lightLevel,
                    visible = visible,
                    explored = explored
                };
            }
        }
        return precalculated;
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int visibleCols, int visibleRows,
        float shakeX, float shakeY, int tileW = 0, int tileH = 0, float fontScale = 0f, bool debugLightOff = false)
    {
        using var _ = new TimeMeasurer("GameWorldRenderer.Render");

        if (tileW <= 0) tileW = AsciiDraw.TileWidth;
        if (tileH <= 0) tileH = AsciiDraw.TileHeight;
        if (fontScale <= 0f) fontScale = AsciiDraw.FontScale;

        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        long tick = state.WorldTick;

        // Precalculate tile brightness and colors for the entire viewport in a single pass.
        var precalculated = PrecalculateTiles(state, cameraCenterX, cameraCenterY, visibleCols, visibleRows, debugLightOff);

        // Pass 1: tile backgrounds and foreground glyphs (batched)
        using (new TimeMeasurer("Pass 1: Tiles"))
        {
            r.DrawGlyphGridScreen(shakeX, shakeY, visibleCols, visibleRows, tileW, tileH, fontScale,
                (col, row) =>
                {
                    var precalc = precalculated[row * visibleCols + col];
                    var tile = precalc.tile;
                    var emptyTile = tile.GlyphId == 0;
                    var brightness = precalc.brightness;

                    if (emptyTile || brightness <= 0f)
                    {
                        return new GlyphTile('\0', default, RenderingTheme.Black);
                    }

                    if (tile.Type == TileType.Water || tile.Type == TileType.Lava)
                    {
                        var worldY = cameraCenterY - visibleRows / 2 + row;
                        var worldX = cameraCenterX - visibleCols / 2 + col;
                        // Animate water and lava, making some tiles slightly brighter or darker over time without using a sine wave to avoid uniformity. 
                        // This adds a bit of life to the environment.
                        float variation =
                            (float)((((worldX * 73856093 ^ worldY * 19349663 ^ tick / 20) % 10) - 5) / 10.0f * 0.3);

                        //float brightnessOffset = (float)(Math.Sin(tick * 0.1 + (worldX + worldY) * 0.5) * 0.2);
                        float brightnessOffset = variation;
                        brightness = Math.Clamp(brightness + brightnessOffset, 0f, 1f);
                    }

                    var bgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(tile.BgColor), brightness);
                    var fgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(tile.FgColor), brightness);
                    var glyphId = tile.GlyphId;

                    // Override glyph/color from placed item when present
                    if (tile.PlaceableItemId != 0)
                    {
                        glyphId = PlaceableDefinitions.GetGlyphId(tile.PlaceableItemId, tile.PlaceableItemExtra);
                        fgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(PlaceableDefinitions.GetFgColor(tile.PlaceableItemId, tile.PlaceableItemExtra)), brightness);
                    }

                    // Client-side door glyph override: pick | or - based on surrounding walls
                    if (PlaceableDefinitions.IsDoor(tile.PlaceableItemId))
                    {
                        var worldDoorX = cameraCenterX - visibleCols / 2 + col;
                        var worldDoorY = cameraCenterY - visibleRows / 2 + row;
                        glyphId = GetDoorGlyph(state, worldDoorX, worldDoorY);
                    }

                    var ch = AsciiDraw.GlyphIdToChar(glyphId);
                    return new GlyphTile(ch, fgColor, bgColor);
                });
        }

        // Pass 2: glow effects behind torches and light-emitting tiles (visible only)
        using (new TimeMeasurer("Pass 2: Glow Effects"))
        {
            for (int sx = 0; sx < visibleCols; sx++)
            {
                for (int sy = 0; sy < visibleRows; sy++)
                {
                    var precalc = precalculated[sy * visibleCols + sx];
                    if (!precalc.visible || precalc.lightLevel < 5) continue;

                    if (precalc.tile.GlyphId == TileDefinitions.GlyphTorch)
                    {
                        float cx = sx * tileW + tileW * 0.5f + shakeX;
                        float cy = sy * tileH + tileH * 0.5f + shakeY;
                        float radius = tileW * 2.5f;
                        var inner = new Color4(255, 200, 100, 40);
                        var outer = new Color4(255, 150, 50, 0);
                        r.DrawFilledCircleScreen(cx, cy, radius, inner, outer, radius * 0.3f, 16);
                    }
                    else if (precalc.tile.Type == TileType.Lava)
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
        }

        // Pass 3: entities (players drawn last so they appear on top)
        using (new TimeMeasurer("Pass 3: Entities"))
        {
            ClientEntity? playerEntity = null;

            foreach (var entity in state.Entities.Values)
            {
                if (entity.Z != state.PlayerZ) continue;
                if (entity.GlyphId == TileDefinitions.GlyphPlayer)
                {
                    playerEntity = entity;
                    continue;
                }
                DrawEntity(r, entity, cameraCenterX, cameraCenterY, visibleCols, visibleRows, tileW, tileH, shakeX, shakeY, fontScale, precalculated);
            }

            // Draw player on top of everything
            if (playerEntity != null)
                DrawEntity(r, playerEntity, cameraCenterX, cameraCenterY, visibleCols, visibleRows, tileW, tileH, shakeX, shakeY, fontScale, precalculated);
        }
    }

    private static void DrawEntity(ISpriteRenderer r, ClientEntity entity,
        int cameraCenterX, int cameraCenterY, int visibleCols, int visibleRows,
        int tileW, int tileH, float shakeX, float shakeY, float fontScale,
        PrecalculatedTile[] precalculated)
    {
        var sx = entity.X - (cameraCenterX - visibleCols / 2);
        var sy = entity.Y - (cameraCenterY - visibleRows / 2);

        if (sx < 0 || sx >= visibleCols || sy < 0 || sy >= visibleRows) return;

        var precalc = precalculated[sy * visibleCols + sx];
        var brightness = precalc.brightness;
        if (brightness <= 0f) return;

        var px = sx * tileW + shakeX;
        var py = sy * tileH + shakeY;
        var fgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(entity.FgColor), brightness);
        var ch = AsciiDraw.GlyphIdToChar(entity.GlyphId);
        r.DrawTextScreen(px, py, ch.ToString(), fgColor, fontScale);

        if (entity.MaxHealth > 0 && entity.Health < entity.MaxHealth)
        {
            var ratio = (float)entity.Health / entity.MaxHealth;
            r.DrawRectScreen(px, py - 2, tileW, 2, RenderingTheme.HpBar.WithAlpha(180));
            r.DrawRectScreen(px, py - 2, tileW * ratio, 2, RenderingTheme.HpFill.WithAlpha(180));
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
                if (tile.Type == TileType.Blocked)
                    dotColor = visible ? new Color4(120, 120, 140, 255) : new Color4(50, 50, 60, 255);
                else if (tile.Type == TileType.Lava)
                    dotColor = visible ? new Color4(255, 80, 20, 255) : new Color4(80, 30, 10, 255);
                else if (tile.Type == TileType.Water)
                    dotColor = visible ? new Color4(70, 130, 255, 255) : new Color4(25, 45, 80, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphTorch)
                    dotColor = visible ? new Color4(255, 200, 100, 255) : new Color4(80, 65, 35, 255);
                else if (PlaceableDefinitions.IsDoor(tile.PlaceableItemId))
                    dotColor = visible ? new Color4(180, 130, 60, 255) : new Color4(60, 45, 25, 255);
                else if (PlaceableDefinitions.IsWall(tile.PlaceableItemId))
                    dotColor = visible ? new Color4(120, 120, 140, 255) : new Color4(50, 50, 60, 255);
                else if (tile.GlyphId == TileDefinitions.GlyphStairsDown || tile.GlyphId == TileDefinitions.GlyphStairsUp)
                    dotColor = visible ? new Color4(255, 255, 80, 255) : new Color4(80, 80, 30, 255);
                else if (tile.PlaceableItemId != 0 && PlaceableDefinitions.IsWalkable(tile.PlaceableItemId, tile.PlaceableItemExtra))
                    dotColor = visible ? new Color4(80, 80, 60, 255) : new Color4(30, 30, 25, 255);
                else if (tile.Type == TileType.Floor)
                    dotColor = visible ? new Color4(60, 60, 70, 255) : new Color4(25, 25, 30, 255);
                else
                    dotColor = visible ? new Color4(50, 50, 60, 255) : new Color4(20, 20, 25, 255);

                r.DrawRectScreen(baseX + dx * pixelSize, baseY + dy * pixelSize, pixelSize, pixelSize, dotColor);
            }

        foreach (var entity in state.Entities.Values)
        {
            if (entity.Z != state.PlayerZ) continue;
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

    /// <summary>
    /// Determines the door glyph based on surrounding wall tiles (client-side only).
    /// Walls N/S → vertical |, walls E/W → horizontal -.
    /// </summary>
    private static int GetDoorGlyph(ClientGameState state, int x, int y)
    {
        bool wallN = IsWallLike(state.GetTile(x, y - 1));
        bool wallS = IsWallLike(state.GetTile(x, y + 1));
        bool wallE = IsWallLike(state.GetTile(x + 1, y));
        bool wallW = IsWallLike(state.GetTile(x - 1, y));

        if (wallN && wallS) return TileDefinitions.GlyphDoorVertical;
        if (wallE && wallW) return TileDefinitions.GlyphDoorHorizontal;
        return TileDefinitions.GlyphDoorVertical; // default
    }

    private static bool IsWallLike(TileInfo tile) =>
        tile.Type == TileType.Blocked || PlaceableDefinitions.IsWall(tile.PlaceableItemId);
}
