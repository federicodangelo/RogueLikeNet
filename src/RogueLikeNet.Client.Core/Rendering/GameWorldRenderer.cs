using Engine.Core;
using Engine.Platform;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Renders the game world tile grid: background tiles with FOW, glow effects, and entity sprites.
/// </summary>
public sealed class GameWorldRenderer
{
    private GlyphTile[] _precalculatedGlyphs = Array.Empty<GlyphTile>();
    private float[] _precalculatedBrightness = Array.Empty<float>();
    private List<(Position, TileInfo)> _tilesWithGlow = new List<(Position, TileInfo)>();

    private (GlyphTile[], float[], List<(Position, TileInfo)>) PrecalculateTiles(ClientGameState state, int cameraCenterX, int cameraCenterY, int visibleCols, int visibleRows, bool debugLightOff)
    {
        using var _ = TimeMeasurer.FromMethodName();

        int requiredSize = visibleCols * visibleRows;
        if (_precalculatedGlyphs.Length < requiredSize)
        {
            _precalculatedGlyphs = new GlyphTile[requiredSize];
            _precalculatedBrightness = new float[requiredSize];
        }

        _tilesWithGlow.Clear();

        _precalculatedGlyphs.AsSpan().Clear();
        _precalculatedBrightness.AsSpan().Clear();

        int originX = cameraCenterX - visibleCols / 2;
        int originY = cameraCenterY - visibleRows / 2;
        long tick = state.WorldTick;

        state.ForEachTileInBounds(originX, originY, originX + visibleCols - 1, originY + visibleRows - 1, state.PlayerZ,
            (int worldX, int worldY, ref TileInfo tile, int lightLevel, bool visible, bool explored) =>
            {
                int col = worldX - originX;
                int row = worldY - originY;

                var minBrightness = explored ? AsciiDraw.FogBrightness : 0f;
                var brightness =
                    !visible && !explored ? 0f :
                        debugLightOff ? 1f :
                        visible ?
                            Math.Max(AsciiDraw.LightLevelToBrightness(lightLevel), minBrightness) :
                            minBrightness;


                var emptyTile = tile.GlyphId == 0;
                var glyphTile = new GlyphTile('\0', default, RenderingTheme.Black);

                if (!emptyTile && brightness > 0f)
                {
                    if (tile.Type == TileType.Water || tile.Type == TileType.Lava)
                    {
                        // Animate water and lava, making some tiles slightly brighter or darker over time without using a sine wave to avoid uniformity. 
                        // This adds a bit of life to the environment.
                        float variation =
                            (float)((((worldX * 73856093 ^ worldY * 19349663 ^ tick / 20) % 10) - 5) / 10.0f * 0.3);

                        //float brightnessOffset = (float)(Math.Sin(tick * 0.1 + (worldX + worldY) * 0.5) * 0.2);
                        float brightnessOffset = variation;
                        brightness = Math.Clamp(brightness + brightnessOffset, 0f, 1f);
                    }

                    if ((tile.GlyphId == TileDefinitions.GlyphTorch || tile.Type == TileType.Lava)
                        && visible && lightLevel >= 5)
                    {
                        _tilesWithGlow.Add((Position.FromCoords(col, row, 0), tile));
                    }

                    var bgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(tile.BgColor), brightness);
                    var fgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(tile.FgColor), brightness);
                    var glyphId = tile.GlyphId;

                    // Override glyph/color from placed item when present
                    if (tile.PlaceableItemId != 0)
                    {
                        glyphId = GameData.Instance.Items.GetPlaceableGlyphId(tile.PlaceableItemId, tile.PlaceableItemExtra);
                        fgColor = ColorUtils.ApplyBrightness(ColorUtils.IntToColor4(GameData.Instance.Items.GetPlaceableFgColor(tile.PlaceableItemId, tile.PlaceableItemExtra)), brightness);
                    }

                    // Client-side door glyph override: pick | or - based on surrounding walls
                    if (GameData.Instance.Items.IsPlaceableDoor(tile.PlaceableItemId))
                    {
                        var worldDoorX = cameraCenterX - visibleCols / 2 + col;
                        var worldDoorY = cameraCenterY - visibleRows / 2 + row;
                        glyphId = GetDoorGlyph(state, tile.PlaceableItemExtra, worldDoorX, worldDoorY);
                    }

                    var ch = AsciiDraw.GlyphIdToChar(glyphId);
                    glyphTile = new GlyphTile(ch, fgColor, bgColor);
                }

                _precalculatedGlyphs[row * visibleCols + col] = glyphTile;
                _precalculatedBrightness[row * visibleCols + col] = brightness;
            });

        return (_precalculatedGlyphs, _precalculatedBrightness, _tilesWithGlow);
    }

    public void Render(ISpriteRenderer r, ClientGameState state, int visibleCols, int visibleRows,
        float shakeX, float shakeY, int tileW = 0, int tileH = 0, float fontScale = 0f, bool debugLightOff = false)
    {
        using var _ = new TimeMeasurer("GameWorldRenderer.Render");

        _tilesWithGlow.Clear();

        if (tileW <= 0) tileW = AsciiDraw.TileWidth;
        if (tileH <= 0) tileH = AsciiDraw.TileHeight;
        if (fontScale <= 0f) fontScale = AsciiDraw.FontScale;

        int cameraCenterX = state.PlayerX;
        int cameraCenterY = state.PlayerY;
        long tick = state.WorldTick;

        // Precalculate tile brightness and colors for the entire viewport in a single pass.
        var (precalculatedGlyphs, precalculatedBrightness, tilesWithGlow) = PrecalculateTiles(state, cameraCenterX, cameraCenterY, visibleCols, visibleRows, debugLightOff);

        // Pass 1: tile backgrounds and foreground glyphs (batched)
        using (new TimeMeasurer("Pass 1: Tiles"))
        {
            r.DrawGlyphGridScreen(shakeX, shakeY, visibleCols, visibleRows, tileW, tileH, fontScale, precalculatedGlyphs);
        }

        // Pass 2: glow effects behind torches and light-emitting tiles (visible only)
        using (new TimeMeasurer("Pass 2: Glow Effects"))
        {
            foreach (var (pos, tile) in tilesWithGlow)
            {
                var sx = pos.X;
                var sy = pos.Y;

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

        // Pass 3: entities (players drawn last so they appear on top)
        using (new TimeMeasurer("Pass 3: Entities"))
        {
            ClientEntity? playerEntity = null;

            foreach (var entity in state.Entities.Values)
            {
                if (entity.Z != state.PlayerZ) continue;
                if (entity.Id == state.PlayerEntityId)
                {
                    playerEntity = entity;
                    continue;
                }
                DrawEntity(r, entity, cameraCenterX, cameraCenterY, visibleCols, visibleRows, tileW, tileH, shakeX, shakeY, fontScale, precalculatedBrightness);
            }

            // Draw player on top of everything
            if (playerEntity != null)
                DrawEntity(r, playerEntity, cameraCenterX, cameraCenterY, visibleCols, visibleRows, tileW, tileH, shakeX, shakeY, fontScale, precalculatedBrightness);
        }
    }

    private static void DrawEntity(ISpriteRenderer r, ClientEntity entity,
        int cameraCenterX, int cameraCenterY, int visibleCols, int visibleRows,
        int tileW, int tileH, float shakeX, float shakeY, float fontScale,
        float[] precalculatedBrightness)
    {
        var sx = entity.X - (cameraCenterX - visibleCols / 2);
        var sy = entity.Y - (cameraCenterY - visibleRows / 2);

        if (sx < 0 || sx >= visibleCols || sy < 0 || sy >= visibleRows) return;

        var brightness = precalculatedBrightness[sy * visibleCols + sx];
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

    /// <summary>
    /// Determines the door glyph based on surrounding wall tiles (client-side only).
    /// Walls N/S → vertical |, walls E/W → horizontal -.
    /// </summary>
    private static int GetDoorGlyph(ClientGameState state, int extra, int x, int y)
    {
        if (extra > 0)
        {
            // Open door!
            return TileDefinitions.GlyphDoor;
        }

        bool wallN = IsWallLike(state.GetTile(x, y - 1));
        bool wallS = IsWallLike(state.GetTile(x, y + 1));
        bool wallE = IsWallLike(state.GetTile(x + 1, y));
        bool wallW = IsWallLike(state.GetTile(x - 1, y));

        if (wallN && wallS) return TileDefinitions.GlyphDoorVertical;
        if (wallE && wallW) return TileDefinitions.GlyphDoorHorizontal;
        return TileDefinitions.GlyphDoorVertical; // default
    }

    private static bool IsWallLike(TileInfo tile) =>
        tile.Type == TileType.Blocked || GameData.Instance.Items.IsPlaceableWall(tile.PlaceableItemId);
}
