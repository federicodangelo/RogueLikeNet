using Engine.Platform;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.Rendering.Menus;

namespace RogueLikeNet.Client.Core.Screens.Menus;

/// <summary>
/// Main menu screen — play offline/online, help, quit.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly MainMenuRenderer _menuRenderer;

    private int _menuIndex;

    public ScreenState ScreenState => ScreenState.MainMenu;

    public MainMenuScreen(ScreenContext ctx, MainMenuRenderer menuRenderer)
    {
        _ctx = ctx;
        _menuRenderer = menuRenderer;
    }

    public void HandleInput(IInputManager input)
    {
        int itemCount = 6;
        InputHelpers.HandleListNavigation(input, ref _menuIndex, itemCount);

        if (input.IsActionPressed(InputAction.MenuConfirm))
        {
            switch (_menuIndex)
            {
                case MainMenuRenderer.MainMenuPlayOfflineIndex:
                    SetClassSelectOnline(false);
                    _ctx.OnPlayOffline();
                    break;
                case MainMenuRenderer.MainMenuPlayOnlineIndex:
                    SetClassSelectOnline(true);
                    _ctx.RequestTransition(Rendering.ScreenState.Login);
                    break;
                case MainMenuRenderer.MainMenuAdminOnlineIndex:
                    _ctx.OnAdminOnline();
                    break;
                case MainMenuRenderer.MainMenuOptionsIndex: _ctx.RequestTransition(Rendering.ScreenState.Options); break;
                case MainMenuRenderer.MainMenuHelpIndex: _ctx.RequestTransition(Rendering.ScreenState.MainMenuHelp); break;
                case MainMenuRenderer.MainMenuQuitIndex: _ctx.OnQuit(); break;
            }
        }
    }

    public void Update(float deltaTime) { }

    public void Render(ISpriteRenderer renderer, int totalCols, int totalRows)
    {
        _menuRenderer.RenderMainMenu(renderer, totalCols, totalRows, _menuIndex);
    }

    public void ResetMenuIndex()
    {
        _menuIndex = 0;
    }

    // Bridge to ClassSelectScreen — stored here temporarily until transition
    internal bool IsOnlineSelected { get; private set; }
    private void SetClassSelectOnline(bool online) => IsOnlineSelected = online;
}
