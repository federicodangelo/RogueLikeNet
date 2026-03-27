using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Help screen — shows controls. Handles both MainMenuHelp and PausedHelp states.
/// </summary>
public sealed class HelpScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;
    private readonly PlayingScreen? _playingScreen;

    private ScreenState _returnTo;
    private ScreenState _screenState;

    public ScreenState ScreenState => _screenState;

    public HelpScreen(ScreenContext ctx, MenuRenderer menuRenderer, PlayingScreen? playingScreen)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
        _playingScreen = playingScreen;
    }

    public void SetMode(ScreenState helpState, ScreenState returnTo)
    {
        _screenState = helpState;
        _returnTo = returnTo;
    }

    public void HandleInput(IInputManager input)
    {
        if (input.IsActionPressed(InputAction.MenuBack) || input.IsActionPressed(InputAction.MenuConfirm))
            _ctx.RequestTransition(_returnTo);
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        bool isOverlay = _screenState == Rendering.ScreenState.PausedHelp;

        if (isOverlay && _playingScreen != null)
            _playingScreen.Render(renderer, totalCols, totalRows);

        _menuRenderer.RenderHelp(renderer, totalCols, totalRows, isOverlay: isOverlay);
    }
}
