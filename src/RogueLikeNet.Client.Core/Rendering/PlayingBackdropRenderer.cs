using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering.Game;
using RogueLikeNet.Client.Core.Screens;

namespace RogueLikeNet.Client.Core.Rendering;

/// <summary>
/// Shared backdrop used by all playing-related screens (<see cref="PlayingScreen"/> and
/// <see cref="PlayingOverlayScreen"/> subclasses). Encapsulates the duplicated
/// black-fill + zoomed game world + particle rendering sequence, and the associated
/// zoom/shake math so individual screens don't repeat it.
/// </summary>
public sealed class PlayingBackdropRenderer
{
    private readonly GameWorldRenderer _worldRenderer;

    public PlayingBackdropRenderer(GameWorldRenderer worldRenderer)
    {
        _worldRenderer = worldRenderer;
    }

    /// <summary>
    /// Clears the screen and renders the zoomed game world (without particles or HUD).
    /// Returns the number of columns reserved for the game area (to the left of the HUD panel).
    /// </summary>
    public int RenderWorld(ISpriteRenderer renderer, ScreenContext ctx, int totalCols, int totalRows)
    {
        int gameCols = totalCols - AsciiDraw.HudColumns;
        var debug = ctx.Debug;
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;
        float fontScale = debug.EffectiveFontScale;

        int gamePixelW = gameCols * AsciiDraw.TileWidth;
        int gamePixelH = totalRows * AsciiDraw.TileHeight;
        int zoomedGameCols = Math.Max(1, gamePixelW / tileW);
        int zoomedRows = Math.Max(1, gamePixelH / tileH);

        float shakeX = ctx.ScreenShake.OffsetX;
        float shakeY = ctx.ScreenShake.OffsetY;

        renderer.DrawRectScreen(0, 0, totalCols * AsciiDraw.TileWidth, totalRows * AsciiDraw.TileHeight, RenderingTheme.Black);
        bool debugLightOff = debug is { Enabled: true, LightOff: true };
        _worldRenderer.Render(renderer, ctx.GameState, zoomedGameCols, zoomedRows, shakeX, shakeY, tileW, tileH, fontScale, debugLightOff);
        return gameCols;
    }

    /// <summary>Renders the particle system centered on the player, matching the same zoom/shake as <see cref="RenderWorld"/>.</summary>
    public void RenderParticles(ISpriteRenderer renderer, ScreenContext ctx, int totalCols, int totalRows)
    {
        int gameCols = totalCols - AsciiDraw.HudColumns;
        var debug = ctx.Debug;
        int tileW = debug.EffectiveTileWidth;
        int tileH = debug.EffectiveTileHeight;

        int gamePixelW = gameCols * AsciiDraw.TileWidth;
        int gamePixelH = totalRows * AsciiDraw.TileHeight;
        int zoomedGameCols = Math.Max(1, gamePixelW / tileW);
        int zoomedRows = Math.Max(1, gamePixelH / tileH);

        int halfW = zoomedGameCols / 2;
        int halfH = zoomedRows / 2;
        float shakeX = ctx.ScreenShake.OffsetX;
        float shakeY = ctx.ScreenShake.OffsetY;

        ctx.Particles.Render(renderer, ctx.GameState.PlayerX, ctx.GameState.PlayerY,
            halfW, halfH, shakeX, shakeY, tileW, tileH);
    }
}
