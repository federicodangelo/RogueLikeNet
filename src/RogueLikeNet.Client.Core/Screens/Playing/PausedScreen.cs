using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Overlays;

namespace RogueLikeNet.Client.Core.Screens.Playing;

/// <summary>
/// Pause menu screen — resume, help, return to main menu. Renders game world behind overlay.
/// </summary>
public sealed class PausedScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly PlayingScreen _playingScreen;
    private readonly PausedRenderer _menuRenderer;

    private int _pauseIndex;

    public ScreenState ScreenState => ScreenState.Paused;

    public PausedScreen(ScreenContext ctx, PlayingScreen playingScreen, PausedRenderer menuRenderer)
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

        int itemCount = 4;
        if (InputHelpers.HandleListNavigation(input, ref _pauseIndex, itemCount))
            return;

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_pauseIndex)
            {
                case PausedRenderer.PauseMenuResumeIndex: _ctx.RequestTransition(Rendering.ScreenState.Playing); break;
                case PausedRenderer.PauseMenuOptionsIndex: _ctx.RequestTransition(Rendering.ScreenState.PausedOptions); break;
                case PausedRenderer.PauseMenuHelpIndex: _ctx.RequestTransition(Rendering.ScreenState.PausedHelp); break;
                case PausedRenderer.PauseMenuMainMenuIndex: _ctx.OnReturnToMenu(); break;
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
