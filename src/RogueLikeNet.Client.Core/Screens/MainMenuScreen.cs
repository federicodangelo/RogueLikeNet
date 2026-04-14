using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;

namespace RogueLikeNet.Client.Core.Screens;

/// <summary>
/// Main menu screen — play offline/online, help, quit.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MenuRenderer _menuRenderer;

    private int _menuIndex;

    public ScreenState ScreenState => ScreenState.MainMenu;

    public MainMenuScreen(ScreenContext ctx, MenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void HandleInput(IInputManager input)
    {
        int itemCount = 6;
        if (input.IsActionPressedOrRepeated(InputAction.MenuUp))
            _menuIndex = (_menuIndex + itemCount - 1) % itemCount;
        else if (input.IsActionPressedOrRepeated(InputAction.MenuDown))
            _menuIndex = (_menuIndex + 1) % itemCount;

        if (_menuIndex == MenuRenderer.MainMenuDebugModeIndex)
        {
            if (input.IsActionPressed(InputAction.MoveLeft) || input.IsActionPressed(InputAction.MoveRight))
                _ctx.Debug.Enabled = !_ctx.Debug.Enabled;
        }

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_menuIndex)
            {
                case MenuRenderer.MainMenuPlayOfflineIndex:
                    SetClassSelectOnline(false);
                    _ctx.OnPlayOffline();
                    break;
                case MenuRenderer.MainMenuPlayOnlineIndex:
                    SetClassSelectOnline(true);
                    _ctx.RequestTransition(Rendering.ScreenState.ClassSelect);
                    break;
                case MenuRenderer.MainMenuAdminOnlineIndex:
                    _ctx.OnAdminOnline();
                    break;
                case MenuRenderer.MainMenuDebugModeIndex: _ctx.Debug.Enabled = !_ctx.Debug.Enabled; break;
                case MenuRenderer.MainMenuHelpIndex: _ctx.RequestTransition(Rendering.ScreenState.MainMenuHelp); break;
                case MenuRenderer.MainMenuQuitIndex: _ctx.OnQuit(); break;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderMainMenu(renderer, totalCols, totalRows, _menuIndex, _ctx.Debug.Enabled);
    }

    public void ResetMenuIndex()
    {
        _menuIndex = 0;
    }

    // Bridge to ClassSelectScreen — stored here temporarily until transition
    internal bool IsOnlineSelected { get; private set; }
    private void SetClassSelectOnline(bool online) => IsOnlineSelected = online;
}
