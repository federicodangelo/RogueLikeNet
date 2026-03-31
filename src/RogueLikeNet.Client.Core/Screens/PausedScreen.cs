using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Pause menu screen — resume, help, return to main menu. Renders game world behind overlay.
/// </summary>
public sealed class PausedScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly PlayingScreen _playingScreen;
    private readonly MenuRenderer _menuRenderer;

    private int _pauseIndex;

    public ScreenState ScreenState => ScreenState.Paused;

    public PausedScreen(ScreenContext ctx, PlayingScreen playingScreen, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _playingScreen = playingScreen;
        _menuRenderer = menuRenderer;
    }

    public void OnEnter()
    {
        _pauseIndex = 0;
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack))
        {
            _ctx.RequestTransition(Rendering.ScreenState.Playing);
            return;
        }

        // Debug key toggles (only in debug mode)
        _ctx.Debug.HandleDebugKeys(input, _ctx.DebugSyncRequested);

        if (input.IsActionPressed(InputAction.MenuUp))
            _pauseIndex = (_pauseIndex + 2) % 3;
        else if (input.IsActionPressed(InputAction.MenuDown))
            _pauseIndex = (_pauseIndex + 1) % 3;
        else if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_pauseIndex)
            {
                case MenuRenderer.PauseMenuResumeIndex: _ctx.RequestTransition(Rendering.ScreenState.Playing); break;
                case MenuRenderer.PauseMenuHelpIndex: _ctx.RequestTransition(Rendering.ScreenState.PausedHelp); break;
                case MenuRenderer.PauseMenuMainMenuIndex: _ctx.OnReturnToMenu(); break;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        // Render the game world behind the overlay
        _playingScreen.Render(renderer, totalCols, totalRows);
        _menuRenderer.RenderPauseOverlay(renderer, totalCols, totalRows, _pauseIndex, _ctx.Debug);
    }
}
